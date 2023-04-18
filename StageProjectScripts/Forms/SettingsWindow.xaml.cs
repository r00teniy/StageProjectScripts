using System.Windows;
using System.Windows.Controls;

namespace StageProjectScripts.Forms
{
    /// <summary>
    /// Interaction logic for SettingsWindow.xaml
    /// </summary>
    public partial class SettingsWindow : Window
    {
        private TextBox[] pavementLabelBoxList;
        private TextBox[] curbsNumberOfLinesBoxList;
        private TextBox[] greeneryLabelBoxList;
        private TextBox[] greeneryGroupingDistanceBoxList;
        private Variables _variables;
        public SettingsWindow(ref Variables variables)
        {
            InitializeComponent();
            _variables = variables;
            pavementLabelBoxList = new TextBox[] { Pave0Box, Pave1Box, Pave2Box, Pave3Box, Pave4Box, Pave5Box, Pave6Box, Pave7Box, Pave8Box, Pave9Box };
            curbsNumberOfLinesBoxList = new TextBox[] { Curb0Box, Curb1Box, Curb2Box, Curb3Box, null, null, Curb6Box, Curb7Box, Curb8Box, Curb9Box };
            greeneryLabelBoxList = new TextBox[] { GreeneryLabel0Box, GreeneryLabel1Box };
            greeneryGroupingDistanceBoxList = new TextBox[] { GreeneryRange0Box, GreeneryRange1Box };
            for (var i = 0; i < pavementLabelBoxList.Length; i++)
            {
                pavementLabelBoxList[i].Text = variables.PLabelValues[i];
            }
            for (var i = 0; i < curbsNumberOfLinesBoxList.Length; i++)
            {
                curbsNumberOfLinesBoxList[i].Text = variables.CurbLineCount[i].ToString();
            }
            for (var i = 0; i < greeneryLabelBoxList.Length; i++)
            {
                greeneryLabelBoxList[i].Text = variables.GreeneryId[i];
            }
            for (var i = 0; i < greeneryGroupingDistanceBoxList.Length; i++)
            {
                greeneryGroupingDistanceBoxList[i].Text = variables.GreeneryGroupingDistance[i].ToString();
            }
        }

        private void CloseParams_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
