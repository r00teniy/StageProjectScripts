using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StageProjectScripts.Forms;
using StageProjectScripts.Functions;
using System.Collections.Generic;
using System.Linq;
using System.Windows;

namespace StageProjectScripts.Models;
public partial class MainWindowViewModel : ObservableObject
{
    internal DataImport _dataImport;
    internal Variables _variables;
    internal SettingsStorage _settingsStorage;
    [ObservableProperty]
    private List<string> _baseXrefs;
    [ObservableProperty]
    private List<string> _plotsXrefs;
    [ObservableProperty]
    private List<string> _plots;
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CalculateButtonClickCommand))]
    private int _selectedPlotsXref;
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CalculateButtonClickCommand))]
    [NotifyCanExecuteChangedFor(nameof(LabelPavementsButtonClickCommand))]
    private int _selectedBaseXref;
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CalculateButtonClickCommand))]
    private int _selectedPlot;
    private MainWindow _mainWindow;
    partial void OnSelectedPlotsXrefChanged(int value)
    {
        Plots = _dataImport.GetAllLayersContainingString(_variables.PlotLayer, PlotsXrefs[value]).Select(x => x.Replace('_', ':')).ToList();
        if (Plots.Count != 0)
        {
            SelectedPlot = 0;
        }
    }

    public MainWindowViewModel(MainWindow mainWindow)
    {
        _mainWindow = mainWindow;
        _settingsStorage = new SettingsStorage();
        _dataImport = new DataImport();
        _variables = _settingsStorage.ReadSettingsFromXML();
        _variables.SavedData = _settingsStorage.ReadData();
        //Base xRef List
        BaseXrefs = new(_dataImport.GetXRefList());
        if (_variables.SavedData[0] != null && BaseXrefs.Where(x => x == _variables.SavedData[0]).ToList().Count != 0)
        {
            SelectedBaseXref = BaseXrefs.IndexOf(_variables.SavedData[0]);
        }
        //Plots xRefList
        PlotsXrefs = BaseXrefs;
        if (_variables.SavedData[1] != null && PlotsXrefs.Where(x => x == _variables.SavedData[1]).ToList().Count != 0)
        {
            SelectedPlotsXref = PlotsXrefs.IndexOf(_variables.SavedData[1]);
            Plots = new(_dataImport.GetAllLayersContainingString(_variables.PlotLayer, _variables.SavedData[1]).Select(x => x.Replace('_', ':')).ToList());
        }
        //Plots

        if (_variables.SavedData[2] != null && Plots.Where(x => x == _variables.SavedData[2]).ToList().Count != 0)
        {
            SelectedPlotsXref = Plots.IndexOf(_variables.SavedData[2]);
        }
    }
    [RelayCommand(CanExecute = nameof(CanCalculateButtonClick))]
    private void CalculateButtonClick()
    {
        _mainWindow.Hide();
        var dataProcessing = new DataProcessing();
        dataProcessing.CalculateVolumes(_variables, BaseXrefs[SelectedBaseXref], PlotsXrefs[SelectedPlotsXref], Plots[SelectedPlot]);
        var settingsStorage = new SettingsStorage();
        settingsStorage.SaveData(BaseXrefs[SelectedBaseXref], PlotsXrefs[SelectedPlotsXref], Plots[SelectedPlot]);
        _mainWindow.Show();
        MessageBox.Show($"Расчет объемов произведен.", "Сообщение", MessageBoxButton.OK);
    }
    private bool CanCalculateButtonClick()
    {
        if (SelectedPlotsXref >= 0 && SelectedBaseXref >= 0 && SelectedPlot >= 0)
            return true;

        return false;
    }

    [RelayCommand(CanExecute = nameof(CanLabelPavementsButtonClick))]
    private void LabelPavementsButtonClick()
    {
        _mainWindow.Hide();
        var dataProcessing = new DataProcessing();
        dataProcessing.LabelPavements(_variables, BaseXrefs[SelectedBaseXref]);
        _mainWindow.Show();
        MessageBox.Show($"Покрытия подписаны.", "Сообщение", MessageBoxButton.OK);
    }
    private bool CanLabelPavementsButtonClick()
    {
        if (SelectedBaseXref >= 0)
            return true;
        return false;
    }

    [RelayCommand]
    private void LabelGreeneryButtonClick()
    {
        _mainWindow.Hide();
        var dataProcessing = new DataProcessing();
        dataProcessing.LabelGreenery(_variables);
        _mainWindow.Show();
        MessageBox.Show($"Озеленение подписано.", "Сообщение", MessageBoxButton.OK);
    }
    [RelayCommand]
    private void SettingsButtonClick()
    {
        SettingsWindow SW = new(ref _variables);
        SW.Show();
    }
}
