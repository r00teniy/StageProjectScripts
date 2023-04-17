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
        public Variables Variables { get; set; }
        public ChecksWindow(Variables variables)
        {
            InitializeComponent();
            Variables = variables;
            //Plots xRefList
            plotsXRefComboBox.SetBinding(ItemsControl.ItemsSourceProperty, new Binding() { Source = plotRef });
            if (Variables.SavedData[1] != "" && plotRef.Where(x => x == Variables.SavedData[1]).ToList().Count != 0)
            {
                plotsXRefComboBox.SelectedIndex = plotRef.IndexOf(Variables.SavedData[1]);
                plots = DataImport.GetAllLayersContainingString(Variables.PlotLayer, plotsXRefComboBox.SelectedItem.ToString()).Select(x => x.Replace('_', ':')).ToList();
                plotsComboBox.SelectedIndex = 0;
            }
            plotsComboBox.ItemsSource = plots;
            if (Variables.SavedData[2] != "" && plots.Where(x => x == Variables.SavedData[2]).ToList().Count != 0)
            {
                plotsComboBox.SelectedIndex = plots.IndexOf(Variables.SavedData[2]);
            }
        }
        private void plotsXRefComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            plotsComboBox.ItemsSource = DataImport.GetAllLayersContainingString(Variables.PlotLayer, plotsXRefComboBox.SelectedItem.ToString()).Select(x => x.Replace('_', ':')).ToList();
            plotsComboBox.SelectedIndex = 0;
        }

        private void CheckIntersections_Click(object sender, RoutedEventArgs e)
        {
            if (plotsXRefComboBox.SelectedItem != null && plotsComboBox.SelectedItem != null)
            {
                Hide();
                Autodesk.AutoCAD.Internal.Utils.SetFocusToDwgView();
                SettingsStorage.SaveData("", plotsXRefComboBox.SelectedItem.ToString(), plotsComboBox.SelectedItem.ToString());
                DataProcessing.CheckForBorderIntersections(Variables, plotsXRefComboBox.SelectedItem.ToString(), plotsComboBox.SelectedItem.ToString());
                Show();
            }
            else
            {
                MessageBox.Show($"Необходимо выбрать файл границ и номер ГПЗУ", "Ошибка", MessageBoxButton.OK);
            }

        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void CheckHatchSelfIntersections_Click(object sender, RoutedEventArgs e)
        {
            Hide();
            Autodesk.AutoCAD.Internal.Utils.SetFocusToDwgView();
            DataProcessing.CheckHatchesForSelfIntersections(Variables);
            Show();
        }

        private void CheckHatches_Click(object sender, RoutedEventArgs e)
        {
            Hide();
            Autodesk.AutoCAD.Internal.Utils.SetFocusToDwgView();
            DataProcessing.CheckForHatchesWithBorderRestorationErrors(Variables);
            Show();
        }

        private void CheckHatchIntersections_Click_1(object sender, RoutedEventArgs e)
        {
            Hide();
            Autodesk.AutoCAD.Internal.Utils.SetFocusToDwgView();
            DataProcessing.HatchIntersections(Variables);
            Show();
        }

        private void DeleteTempGeometry_Click(object sender, RoutedEventArgs e)
        {
            Hide();
            Autodesk.AutoCAD.Internal.Utils.SetFocusToDwgView();
            DataExport.ClearTemp(Variables);
            Show();
        }
    }
}
