using System;
using System.Threading;
using System.Threading.Tasks;
using Amanhecer.Abstractions;
using Amanhecer.Abstractions.Extensions;
using Amanhecer.Abstractions.Messaging;
using Amanhecer.Abstractions.Messaging.Inboxes;
using Amanhecer.Abstractions.Messaging.Transactions;

namespace Amanhecer.Messaging.Middlewares;

/// <summary>
/// Middleware that provides at-most-once processing through an inbox. When the consumed
/// message is recorded for the first time, the rest of the pipeline runs within the same
/// transaction as the inbox entry; duplicates are skipped and the transaction is committed
/// so the message is still settled.
/// </summary>
/// <param name="accessor">Accessor to the ambient transaction shared with the pipeline.</param>
public class InboxMiddleware(ITransactionAccessor accessor) : IMiddleware
{
    /// <summary>
    /// Records the consumed message in the subscription's inbox and, when it has not been
    /// processed before, invokes the rest of the pipeline under the inbox transaction.
    /// </summary>
    /// <param name="context">The current pipeline context.</param>
    /// <param name="next">The next middleware in the pipeline.</param>
    /// <returns>A <see cref="ValueTask"/> that completes when the pipeline has run.</returns>
    public async ValueTask ExecuteAsync(AmanhecerContext context, Func<AmanhecerContext, ValueTask> next)
    {
        var subscription = context.GetMetadata<ISubscription>(MetadataName.Subscription);
        var inbox = subscription?.Inbox;
        if (inbox == null || context.Request is not Message message)
        {
            await next(context).ConfigureAwait(context.ContinueOnCapturedContext);
            return;
        }

        await using var transaction = await GetOrCreateTransactionAsync(context, inbox)
            .ConfigureAwait(context.ContinueOnCapturedContext);

        var previous = accessor.Transaction;
        accessor.Transaction = transaction.Inner;

        try
        {
            var response = await inbox.StoreAsync(message, transaction, context.CancellationToken)
                .ConfigureAwait(context.ContinueOnCapturedContext);

            if (response == InboxStoreResponse.Inserted)
            {
                // TODO: Add debug log
                await next(context).ConfigureAwait(context.ContinueOnCapturedContext);
            }
            else
            {
                // TODO: Add warn log
                await transaction.RollbackAsync(context.CancellationToken)
                    .ConfigureAwait(context.ContinueOnCapturedContext);
            }

            await transaction.CommitAsync(context.CancellationToken)
                .ConfigureAwait(context.ContinueOnCapturedContext);
        }
        finally
        {
            accessor.Transaction = previous;
        }
    }


    private ValueTask<InnerTransaction> GetOrCreateTransactionAsync(AmanhecerContext context, IInbox inbox)
    {
        var transaction = accessor.Transaction;
        if (transaction != null)
        {
            return new ValueTask<InnerTransaction>(new InnerTransaction(transaction, inbox.AutoCommit, false));
        }

        var metadata = context.GetMetadata<TransactionMetadata>();
        if (metadata != null)
        {
            return new ValueTask<InnerTransaction>(new InnerTransaction(metadata.Transaction, inbox.AutoCommit, false));
        }

        return new ValueTask<InnerTransaction>(CreateTransaction(inbox.TransactionManager, context.CancellationToken));

        static async Task<InnerTransaction> CreateTransaction(ITransactionManager transactionManager, CancellationToken cancellationToken)
        {
            var transaction = await transactionManager.BeginAsync(cancellationToken);
            return new InnerTransaction(transaction, true, true);
        }
    }

    private class InnerTransaction : ITransaction, IAsyncDisposable, IDisposable
    {
        private readonly bool _shouldCommit;
        private readonly bool _shouldDispose;

        public InnerTransaction(ITransaction inner, bool shouldCommit, bool shouldDispose)
        {
            _shouldCommit = shouldCommit;
            _shouldDispose = shouldDispose;
            Inner = inner;
        }

        public ITransaction Inner { get; }
        
        public void Dispose()
        {
            var dispose = DisposeAsync();

            if (!dispose.IsCompletedSuccessfully)
            {
                dispose.GetAwaiter().GetResult();
            }
        }

        public async ValueTask DisposeAsync()
        {
            if (!_shouldDispose)
            {
                return;
            }

            if (Inner is IAsyncDisposable asyncDisposable)
            {
                await asyncDisposable.DisposeAsync();
            }
            else if (Inner is IDisposable disposable)
            {
                disposable.Dispose();
            }
        }

        public async ValueTask CommitAsync(CancellationToken cancellationToken = default)
        {
            if (_shouldCommit)
            {
                await Inner.CommitAsync(cancellationToken);
            }
        }

        public async ValueTask RollbackAsync(CancellationToken cancellationToken = default)
        {
            await Inner.RollbackAsync(cancellationToken);
        }
    }
}