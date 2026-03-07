using Microsoft.UI;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Microsoft.UI;
using Microsoft.UI.Xaml.Media;

namespace ADO_Tools_WinUI.Pages
{
    public class LogEntryViewModel : INotifyPropertyChanged
    {
        private string _message;
        private string _glyph;
        private SolidColorBrush _glyphBrush;

        public string Message
        {
            get => _message;
            set { _message = value; OnPropertyChanged(); }
        }

        public string Glyph
        {
            get => _glyph;
            set { _glyph = value; OnPropertyChanged(); }
        }

        public SolidColorBrush GlyphBrush
        {
            get => _glyphBrush;
            set { _glyphBrush = value; OnPropertyChanged(); }
        }

        public bool IsProgressEntry { get; set; }

        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        public static bool IsProgressMessage(string message)
            => message.StartsWith("Downloaded:", System.StringComparison.OrdinalIgnoreCase);

        public static (string glyph, SolidColorBrush brush) ClassifyMessage(string message)
        {
            return message switch
            {
                _ when message.Contains("error", System.StringComparison.OrdinalIgnoreCase)
                    || message.Contains("failed", System.StringComparison.OrdinalIgnoreCase)
                    || message.Contains("aborted", System.StringComparison.OrdinalIgnoreCase)
                    => ("\xEA39", new SolidColorBrush(Colors.IndianRed)),

                _ when message.Contains("success", System.StringComparison.OrdinalIgnoreCase)
                    || message.Contains("complete", System.StringComparison.OrdinalIgnoreCase)
                    || message.Contains("installed", System.StringComparison.OrdinalIgnoreCase)
                    => ("\xE73E", new SolidColorBrush(Colors.LimeGreen)),

                _ when IsProgressMessage(message)
                    || message.Contains("download", System.StringComparison.OrdinalIgnoreCase)
                    || message.Contains("fetch", System.StringComparison.OrdinalIgnoreCase)
                    => ("\xE896", new SolidColorBrush(Colors.DodgerBlue)),

                _ => ("\xE946", new SolidColorBrush(Colors.Gray)),
            };
        }

        public static LogEntryViewModel Create(string message)
        {
            var (glyph, brush) = ClassifyMessage(message);

            return new LogEntryViewModel
            {
                _message = message,
                _glyph = glyph,
                _glyphBrush = brush,
                IsProgressEntry = IsProgressMessage(message),
            };
        }

        public void Update(string message)
        {
            var (glyph, brush) = ClassifyMessage(message);
            Message = message;
            Glyph = glyph;
            GlyphBrush = brush;
        }
    }
}
