﻿using System;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Forms;
using System.Windows.Media;

namespace GoogleAssistantWindows
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private const int NormalHeight = 350;
        private const int DebugHeight = 600;

        private readonly UserManager _userManager;
        private readonly Assistant _assistant;

        private readonly KeyboardHook _hook;

        private readonly NotifyIcon _notifyIcon;

        private readonly AudioOut _audioOut;

        private ScrollViewer listBoxOutputScrollViewer;

        private AssistantState _assistantState = AssistantState.Inactive;

        private ObservableCollection<DialogResult> dialogResults;

        public MainWindow()
        {
            InitializeComponent();

            _audioOut = new AudioOut();          

            _hook = new KeyboardHook();
            _hook.KeyDown += OnHookKeyDown;
            void OnHookKeyDown(object sender, HookEventArgs e)
            {
                // Global keyboard hook for Ctrl+Alt+G to start listening.
                if (e.Control && e.Alt && e.Key == Keys.G)
                    StartListening();                
            }

            // When minimized it will hide in the tray. but the global keyboard hook should still work
            _notifyIcon = new NotifyIcon();
            _notifyIcon.Icon = new System.Drawing.Icon("Mic.ico");
            _notifyIcon.Text = Title;            
            _notifyIcon.DoubleClick +=
                delegate
                {
                    _notifyIcon.Visible = false;
                    Show();
                    WindowState = WindowState.Normal;
                };

            _assistant = new Assistant();
            _assistant.OnDebug += Output;
            _assistant.OnAssistantStateChanged += OnAssistantStateChanged;
            _assistant.OnAssistantDialogResult += OnAssistantDialogResult;
            _assistant.OnAssistantSpeechResult += OnAssistantSpeechResult;

            _userManager = UserManager.Instance;
            _userManager.OnUserUpdate += OnUserUpdate;

            dialogResults = new ObservableCollection<DialogResult>();
            DialogBox.ItemsSource = dialogResults;
        }

        private void OnAssistantSpeechResult(string message)
        {
            dialogResults.Add(new DialogResult() { Actor = DialogResultActor.User, Message = message });
        }

        private void OnAssistantDialogResult(string message)
        {
            dialogResults.Add(new DialogResult() { Actor = DialogResultActor.GoogleAssistant, Message = message });
        }

        protected override void OnStateChanged(EventArgs e)
        {
            if (WindowState == WindowState.Minimized)
            {
                _notifyIcon.Visible = true;
                Hide();
            }
            base.OnStateChanged(e);
        }
        
        private void OnAssistantStateChanged(AssistantState state)
        {
            _assistantState = state;
            UpdateButtonText(state);
        }

        private void UpdateButtonText(AssistantState state)
        {
            if (ButtonRecord.Dispatcher.CheckAccess())
                ButtonRecord.Content = state == AssistantState.Inactive ? "Press" : state.ToString();
            else
                ButtonRecord.Dispatcher.BeginInvoke(new Action(()=>UpdateButtonText(state)));
        }

        private void OnUserUpdate(UserManager.GoogleUserData userData)
        {
            ButtonRecord.IsEnabled = false;
            _assistant.Shutdown();
            if (userData != null)
            {
                _assistant.InitAssistantForUser(_userManager.GetChannelCredential());
                ButtonRecord.IsEnabled = true;
            }
        }

        private void MainWindow_OnLoaded(object sender, RoutedEventArgs e)
        {
            if (Utils.HasTokenFile()) 
                _userManager.GetOrRefreshCredential();     // we don't need to wait for this UserManager will throw an event on loaded.  

            listBoxOutputScrollViewer = FindVisualChild<ScrollViewer>(ListBoxOutput);

        }

        private void ButtonRecord_OnClick(object sender, RoutedEventArgs e)
        {
            StartListening();
        }

        private void StartListening()
        {
            if (_assistant.IsInitialised() && _assistantState == AssistantState.Inactive)
            {
                _assistant.NewConversation();          
                _audioOut.PlayNotification();
            }
        }

        public void Output(string output, bool consoleOnly = false)
        {
            if (consoleOnly)
            {
                System.Diagnostics.Debug.WriteLine(output);
                return;
            }

            if (ListBoxOutput.Dispatcher.CheckAccess())
            {
                System.Diagnostics.Debug.WriteLine(output);

                // stop using memory for old debug lines.
                if (ListBoxOutput.Items.Count > 500)
                    ListBoxOutput.Items.RemoveAt(0);

                ListBoxOutput.Items.Add(output);
                listBoxOutputScrollViewer.ScrollToBottom(); // fix because ListBoxOutput.ScrollIntoView was not working reliable

                if (output.StartsWith("Error") && Height == NormalHeight)
                    Height = DebugHeight;
            }
            else
                ListBoxOutput.Dispatcher.BeginInvoke(new Action(() => Output(output)));
        }

        private void DebugButton_OnClick(object sender, RoutedEventArgs e)
        {
            Height = (Height == NormalHeight ? DebugHeight : NormalHeight);
        }

        private childItem FindVisualChild<childItem>(DependencyObject obj) where childItem : DependencyObject
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(obj); i++)
            {
                DependencyObject child = VisualTreeHelper.GetChild(obj, i);
                if (child != null && child is childItem)
                {
                    return (childItem)child;
                }
                else
                {
                    childItem childOfChild = FindVisualChild <childItem>(child);
                    if (childOfChild != null)
                    {
                        return childOfChild;
                    }
                }
            }
            return null;
        }


    }
}
