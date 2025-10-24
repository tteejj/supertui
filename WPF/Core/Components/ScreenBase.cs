using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace SuperTUI.Core
{
    /// <summary>
    /// Base class for screens - larger interactive components
    /// </summary>
    public abstract class ScreenBase : UserControl, INotifyPropertyChanged
    {
        public string ScreenName { get; set; }
        public string ScreenType { get; set; }
        public Guid ScreenId { get; private set; } = Guid.NewGuid();

        private bool hasFocus;
        public bool HasFocus
        {
            get => hasFocus;
            set
            {
                if (hasFocus != value)
                {
                    hasFocus = value;
                    OnPropertyChanged(nameof(HasFocus));

                    if (value)
                        OnFocusReceived();
                    else
                        OnFocusLost();
                }
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public ScreenBase()
        {
            this.Focusable = true;
            this.GotFocus += (s, e) => HasFocus = true;
            this.LostFocus += (s, e) => HasFocus = false;
        }

        /// <summary>
        /// Initialize screen - called once when screen is created
        /// </summary>
        public abstract void Initialize();

        /// <summary>
        /// Handle keyboard input
        /// </summary>
        public virtual void OnScreenKeyDown(KeyEventArgs e) { }

        /// <summary>
        /// Called when screen receives focus
        /// </summary>
        public virtual void OnFocusReceived() { }

        /// <summary>
        /// Called when screen loses focus
        /// </summary>
        public virtual void OnFocusLost() { }

        /// <summary>
        /// Check if screen can be closed (return false to prevent)
        /// </summary>
        public virtual bool CanClose() => true;

        /// <summary>
        /// Save screen state
        /// </summary>
        public virtual Dictionary<string, object> SaveState()
        {
            return new Dictionary<string, object>
            {
                ["ScreenName"] = ScreenName,
                ["ScreenType"] = ScreenType,
                ["ScreenId"] = ScreenId
            };
        }

        /// <summary>
        /// Restore screen state
        /// </summary>
        public virtual void RestoreState(Dictionary<string, object> state)
        {
            // Override in derived classes
        }
    }
}
