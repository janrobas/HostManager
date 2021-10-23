namespace HostManager.Models
{
    public class HostModel
    {
        public void Clean()
        {
            this.Dirty = false;
        }

        public bool Dirty { get; private set; }
        public int LineNumber { get; set; }
        public string Address { get; set; }
        public string Host { get; set; }
        public string Comment { get; set; }

        private bool enabled;
        public bool Enabled
        {
            get => this.enabled;
            set
            {
                this.enabled = value;
                this.Dirty = true;
            }
        }
    }
}
