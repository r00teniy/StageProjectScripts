using System.Collections.ObjectModel;
using System.ComponentModel;

namespace StageProjectScripts.Models;
public class MainWindowViewModel : INotifyPropertyChanged
{
    public ObservableCollection<string> BaseXrefs { get; set; }
    public ObservableCollection<string> PlotsXrefs { get; set; }
    public ObservableCollection<string> Plots { get; set; }
    public Variables Variables { get; set; }
    public MainWindowViewModel()
    {
        if (DesignerProperties.GetIsInDesignMode(new System.Windows.DependencyObject()))
        {
            BaseXrefs = new();
            PlotsXrefs = new();
            Plots = new();
        }
        else
        {
            /*var settingsStorage = new SettingsStorage();
            Variables = settingsStorage.ReadSettingsFromXML();
            Variables.SavedData = settingsStorage.ReadData();
            var _dataImport = new DataImport();
            BaseXrefs = new(_dataImport.GetXRefList());
            PlotsXrefs = new(_dataImport.GetXRefList());*/
        }
    }

    public event PropertyChangedEventHandler PropertyChanged;
    private void OnPropertyChange(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
