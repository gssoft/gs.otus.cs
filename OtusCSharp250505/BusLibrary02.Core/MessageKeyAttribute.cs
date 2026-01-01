// MessageKeyAttribute.cs

using System;

namespace BusLibrary02.Core
{
    /// <summary>
    /// Атрибут для указания ключа сообщения
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, Inherited = false, AllowMultiple = false)]
    public sealed class MessageKeyAttribute : Attribute
    {
        public string Key { get; }

        public MessageKeyAttribute(string key)
        {
            Key = key ?? throw new ArgumentNullException(nameof(key));
        }
    }
}