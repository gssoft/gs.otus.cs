// Visualization/TradingLog.cs
namespace TradingPlatform.Visualization
{
    public class TradingLog
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Ticker { get; set; } = string.Empty;
        public string Strategy { get; set; } = string.Empty;
        public string Level { get; set; } = string.Empty; // Information, Warning, Error
        public string Category { get; set; } = string.Empty; // Trade, Deal, Order, Position, System
        public string Message { get; set; } = string.Empty;
        public string FormattedMessage { get; set; } = string.Empty; // С эмодзи и HTML
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }
}
