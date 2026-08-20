using System;

namespace Amanhecer.Abstractions;

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Method)]
public class RequestIdAttribute : Attribute;