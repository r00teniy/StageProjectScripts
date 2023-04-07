using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

using StageProjectScripts.Functions;

namespace StageProjectScripts.Forms
{
    /// <summary>
    /// Interaction logic for ChecksWindow.xaml
    /// </summary>
    public partial class ChecksWindow : Window
    {
        private BindingList<string> plotRef = new(DataImport.GetXRefList());
        private List<string> plots = new();
        public ChecksWindow()
        {
            InitializeComponent();
            //Plots xRefList
            plotsXRefComboBox.SetBinding(ItemsControl.ItemsSourceProperty, new Binding() { Source = plotRef });
            if (Variables.savedData[1] != "" && plotRef.Where(x => x == Variables.savedData[1]).ToList().Count != 0)
            {
                plotsXRefComboBox.SelectedIndex = plotRef.IndexOf(Variables.savedData[1]);
                plots = DataImport.GetAllLayersContainingString(Variables.plotLayers, plotsXRefComboBox.SelectedItem.ToString()).Select(x => x.Replace('_', ':')).ToList();
                plotsComboBox.SelectedIndex = 0;
            }
            plotsComboBox.ItemsSource = plots;
            if (Variables.savedData[2] != "" && plots.Where(x => x == Variables.savedData[2]).ToList().Count != 0)
            {
                plotsComboBox.SelectedIndex = plots.IndexOf(Variables.savedData[2]);
            }
        }
        private void plotsXRefComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            plotsComboBox.ItemsSource = DataImport.GetAllLayersContainingString(Variables.plotLayers, plotsXRefComboBox.SelectedItem.ToString()).Select(x => x.Replace('_', ':')).ToList();
            plotsComboBox.SelectedIndex = 0;
        }

        private void CheckIntersections_Click(object sender, RoutedEventArgs e)
        {
            this.Hide();
            SettingsStorage.SaveData("", plotsXRefComboBox.SelectedItem.ToString(), plotsComboBox.SelectedItem.ToString());
            DataProcessing.CheckForBorderIntersections(plotsXRefComboBox.SelectedItem.ToString(), plotsComboBox.SelectedItem.ToString());
            this.Show();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}
