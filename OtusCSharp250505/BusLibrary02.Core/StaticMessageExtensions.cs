// StaticMessageExtensions.cs

using System;
using System.Collections.Generic;
using System.Text;

namespace BusLibrary02.Core;
/// <summary>
/// Интерфейс для сообщений со статическим ключом
/// </summary>
public interface IStaticKeyMessage
{
    /// <summary>
    /// Статический ключ сообщения (без создания экземпляра)
    /// </summary>
    static abstract string StaticKey { get; }
}

