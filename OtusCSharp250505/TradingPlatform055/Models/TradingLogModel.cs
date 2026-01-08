// Models/TradingLogModel.cs
using System;

namespace TradingPlatform.Visualization
{
    public class TradingLog
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public string Ticker { get; set; } = string.Empty;
        public string Strategy { get; set; } = string.Empty;
        public string Level { get; set; } = "Information"; // Information, Warning, Error
        public string Category { get; set; } = string.Empty; // EventHubStrategy, TradingMonitorService и т.д.
        public string Message { get; set; } = string.Empty;
        public string FormattedMessage { get; set; } = string.Empty; // С эмодзи и форматированием

        // Для быстрого поиска
        public bool ContainsTrade => Message.Contains("TRADE:", StringComparison.OrdinalIgnoreCase);
        public bool ContainsDeal => Message.Contains("DEAL", StringComparison.OrdinalIgnoreCase);
        public bool ContainsPosition => Message.Contains("POSITION", StringComparison.OrdinalIgnoreCase);
        public bool ContainsQuote => Message.Contains("↑") || Message.Contains("↓") || Message.Contains("→");

        // Метод для определения уровня на основе сообщения
        public static string DetermineLevel(string message)
        {
            if (message.Contains("❌") || message.Contains("ERROR", StringComparison.OrdinalIgnoreCase))
                return "Error";
            if (message.Contains("⚠️") || message.Contains("WARNING", StringComparison.OrdinalIgnoreCase))
                return "Warning";
            return "Information";
        }

        // Метод для определения категории на основе сообщения
        public static string DetermineCategory(string message)
        {
            if (message.Contains("TRADE:") || message.Contains("DEAL CLOSED:") || message.Contains("POSITION:"))
                return "EventHubStrategy";
            if (message.Contains("↑") || message.Contains("↓") || message.Contains("→"))
                return "QuotesConsoleService";
            if (message.Contains("Опубликовано событие") || message.Contains("Ошибка при публикации"))
                return "EventHub";
            return "System";
        }
    }
}