using EntParser.ViewModel;
using System;
using System.Collections.Generic;
using System.Linq;
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

namespace EntParser.View
{
    /// <summary>
    /// Interaction logic for EntParser.xaml
    /// </summary>
    public partial class EntParser : Window
    {
        public EntParser()
        {
            InitializeComponent();
            DataContext = new EntParserViewModel();
        }
    }
}
