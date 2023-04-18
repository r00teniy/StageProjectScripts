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
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private BindingList<string> xRefs = new(DataImport.GetXRefList());
        private BindingList<string> plotRef = new(DataImport.GetXRefList());
        private List<string> plots = new();

        public Variables Variables { get; set; }

        public MainWindow(Variables variables)
        {
            InitializeComponent();
            Variables = variables;
            //Base xRef List
            baseXRefComboBox.SetBinding(ItemsControl.ItemsSourceProperty, new Binding() { Source = xRefs });
            if (Variables.SavedData[0] != "" && xRefs.Where(x => x == Variables.SavedData[0]).ToList().Count != 0)
            {
                baseXRefComboBox.SelectedIndex = xRefs.IndexOf(Variables.SavedData[0]);
            }
            //Plots xRefList
            plotsXRefComboBox.SetBinding(ItemsControl.ItemsSourceProperty, new Binding() { Source = plotRef });
            if (Variables.SavedData[1] != "" && plotRef.Where(x => x == Variables.SavedData[1]).ToList().Count != 0)
            {
                plotsXRefComboBox.SelectedIndex = plotRef.IndexOf(Variables.SavedData[1]);
                plots = DataImport.GetAllLayersContainingString(Variables.PlotLayer, plotsXRefComboBox.SelectedItem.ToString()).Select(x => x.Replace('_', ':')).ToList();
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

        private void CalculateButton_Click(object sender, RoutedEventArgs e)
        {
            if (baseXRefComboBox.SelectedItem != null && plotsXRefComboBox.SelectedItem != null && plotsComboBox.SelectedItem != null)
            {
                Hide();
                DataProcessing.CalculateVolumes(Variables, baseXRefComboBox.SelectedItem.ToString(), plotsXRefComboBox.SelectedItem.ToString(), plotsComboBox.SelectedItem.ToString());
                SettingsStorage.SaveData(baseXRefComboBox.SelectedItem.ToString(), plotsXRefComboBox.SelectedItem.ToString(), plotsComboBox.SelectedItem.ToString());
                Show();
            }
            else
            {
                MessageBox.Show($"Необходимо выбрать файл основы, границ и номер ГПЗУ", "Ошибка", MessageBoxButton.OK);
            }
        }
        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void LabelPavement_Click(object sender, RoutedEventArgs e)
        {
            if (baseXRefComboBox.SelectedItem != null)
            {
                Hide();
                DataProcessing.LabelPavements(Variables, baseXRefComboBox.SelectedItem.ToString());
                Show();
            }
            else
            {
                MessageBox.Show($"Необходимо выбрать файл основы", "Ошибка", MessageBoxButton.OK);
            }
        }

        private void LabelGreenery_Click(object sender, RoutedEventArgs e)
        {
            if (baseXRefComboBox.SelectedItem != null)
            {
                Hide();
                DataProcessing.LabelGreenery(Variables);
                Show();
            }
            else
            {
                MessageBox.Show($"Необходимо выбрать файл основы", "Ошибка", MessageBoxButton.OK);
            }
        }

        private void SettingsButton_Click(object sender, RoutedEventArgs e)
        {

        }
    }
}
