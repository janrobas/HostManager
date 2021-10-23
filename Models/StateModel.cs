using System.Collections.ObjectModel;

namespace HostManager.Models
{
    /// <summary>
    /// Class to hold application state.
    /// </summary>
    public class StateModel
    {
        public ObservableCollection<HostModel> HostCollection { get; set; } = new();
    }
}
