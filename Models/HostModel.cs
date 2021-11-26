using System.ComponentModel;
using System.Net;

namespace HostManager.Models
{
    public class HostModel : INotifyPropertyChanged
    {
        public void Clean()
        {
            this.Dirty = false;
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string name = null)
        {
            this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        public double opacity = 1;
        public double Opacity
        {
            get
            {
                return this.opacity;
            }
            private set
            {
                this.opacity = value;
                this.OnPropertyChanged("Opacity");
            }
        }

        public bool dirty = false;
        public bool Dirty
        {
            get
            {
                return this.dirty;
            }
            private set
            {
                this.dirty = value;
                this.Opacity = value ? 0.5 : 1;
            }
        }
        public int? LineNumber { get; set; }


        public string address = "127.0.0.1";
        public string Address
        {
            get => this.address;
            set
            {
                this.address = value;
                this.Dirty = true;
            }
        }

        public string host;
        public string Host
        {
            get => this.host;
            set
            {
                this.host = value;
                this.Dirty = true;
            }
        }

        public string comment;
        public string Comment
        {
            get => this.comment;
            set
            {
                this.comment = value;
                this.Dirty = true;
            }
        }

        private bool enabled = true;

        public bool Enabled
        {
            get => this.enabled;
            set
            {
                this.enabled = value;
                this.Dirty = true;
            }
        }

        private bool deleted = false;
        public bool Deleted
        {
            get
            {
                return this.deleted;
            }
            set
            {
                this.deleted = value;
                this.Opacity = value ? 0.5 : 1;
            }
        }

        public override string ToString()
        {
            return $"{(Enabled ? "" : "#")}{Address,-16} {Host,-24} {(string.IsNullOrEmpty(Comment) ? "" : $"# {Comment}")}";
        }

        public bool IsValid => !string.IsNullOrWhiteSpace(this.host) && IPAddress.TryParse(this.address, out _);
    }
}
