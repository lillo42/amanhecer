namespace Amanhecer.Abstractions.Messaging.Inboxes;

/// <summary>
/// The result of storing a message in an <see cref="IInbox"/>.
/// </summary>
public enum InboxStoreResponse
{
   /// <summary>
   /// The message was recorded for the first time and should be processed.
   /// </summary>
   Inserted,

   /// <summary>
   /// The message was already recorded and processed, and should be skipped.
   /// </summary>
   AlreadyProcess
}