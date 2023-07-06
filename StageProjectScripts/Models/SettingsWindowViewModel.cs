using CommunityToolkit.Mvvm.ComponentModel;


namespace StageProjectScripts.Models
{
    public partial class SettingsWindowViewModel : ObservableObject
    {
        [ObservableProperty]
        internal Variables _variables;
        public SettingsWindowViewModel(ref Variables variables)
        {
            _variables = variables;

        }
    }
}
