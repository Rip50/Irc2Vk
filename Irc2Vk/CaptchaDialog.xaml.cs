using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace Irc2Vk
{
    /// <summary>
    /// Interaction logic for CaptchaDialog.xaml
    /// </summary>
    public partial class CaptchaDialog : Window, INotifyPropertyChanged
    {
        public BitmapFrame Image { get; private set; }
        public CaptchaDialog()
        { 
            Text = "";
            InitializeComponent();
        }

        public string Text { get; private set; }

        public void SetBitmap(BitmapFrame frame)
        {
            Image = frame;
            OnPropertyChanged("Image");
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected  void OnPropertyChanged(string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private void OkClicked(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }

        private void OnTextChanged(object sender, TextChangedEventArgs e)
        {
            var textBox = e.OriginalSource as TextBox;
            if (textBox != null)
                Text = textBox.Text;
        }
    }
}
