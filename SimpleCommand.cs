using System;
using System.Windows.Input;

namespace HostManager
{
    public class SimpleCommand : ICommand
    {
        public SimpleCommand(Action action)
        {
            this.action = action;
        }

        public event EventHandler CanExecuteChanged;
        private readonly Action action;

        public bool CanExecute(object parameter)
        {
            return true;
        }

        public void Execute(object parameter)
        {
            action();
        }
    }
}
