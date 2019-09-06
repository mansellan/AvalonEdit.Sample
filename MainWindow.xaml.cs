// Copyright (c) 2009 Daniel Grunwald
// 
// Permission is hereby granted, free of charge, to any person obtaining a copy of this
// software and associated documentation files (the "Software"), to deal in the Software
// without restriction, including without limitation the rights to use, copy, modify, merge,
// publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons
// to whom the Software is furnished to do so, subject to the following conditions:
// 
// The above copyright notice and this permission notice shall be included in all copies or
// substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED,
// INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR
// PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE
// FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR
// OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER
// DEALINGS IN THE SOFTWARE.

using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using System.Xml;

using ICSharpCode.AvalonEdit.CodeCompletion;
using ICSharpCode.AvalonEdit.Folding;
using ICSharpCode.AvalonEdit.Highlighting;
using Microsoft.Win32;

namespace ICSharpCode.AvalonEdit.Sample
{
    /// <summary>
    /// Interaction logic for Window1.xaml
    /// </summary>
    public partial class MainWindow
    {
        public MainWindow()
        {
            // Load our custom highlighting definition
            IHighlightingDefinition customHighlighting;
            using (var s = typeof(MainWindow).Assembly.GetManifestResourceStream("AvalonEdit.Sample.CustomHighlighting.xshd"))
            {
                if (s == null)
                    throw new InvalidOperationException("Could not find embedded resource");
                using var reader = new XmlTextReader(s);
                customHighlighting = ICSharpCode.AvalonEdit.Highlighting.Xshd.
                HighlightingLoader.Load(reader, HighlightingManager.Instance);
            }
            // and register it in the HighlightingManager
            HighlightingManager.Instance.RegisterHighlighting("Custom Highlighting", new[] { ".cool" }, customHighlighting);


            InitializeComponent();
            propertyGridComboBox.SelectedIndex = 2;

            //textEditor.SyntaxHighlighting = HighlightingManager.Instance.GetDefinition("C#");
            //textEditor.SyntaxHighlighting = customHighlighting;
            // initial highlighting now set by XAML

            textEditor.TextArea.TextEntering += textEditor_TextArea_TextEntering;
            textEditor.TextArea.TextEntered += TextEditor_TextArea_TextEntered;

            var foldingUpdateTimer = new DispatcherTimer {Interval = TimeSpan.FromSeconds(2)};
            foldingUpdateTimer.Tick += delegate { UpdateFoldings(); };
            foldingUpdateTimer.Start();
        }

        string _currentFileName;

        private void OpenFile_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog {CheckFileExists = true};
            if (!(dlg.ShowDialog() ?? false))
            {
                return;
            }

            _currentFileName = dlg.FileName;
            textEditor.Load(_currentFileName);
            textEditor.SyntaxHighlighting = HighlightingManager.Instance.GetDefinitionByExtension(Path.GetExtension(_currentFileName));
        }

        void SaveFile_Click(object sender, EventArgs e)
        {
            if (_currentFileName == null)
            {
                var dlg = new SaveFileDialog {DefaultExt = ".txt"};
                if (dlg.ShowDialog() ?? false)
                {
                    _currentFileName = dlg.FileName;
                }
                else
                {
                    return;
                }
            }
            textEditor.Save(_currentFileName);
        }

        private void PropertyGridComboBox_SelectionChanged(object sender, RoutedEventArgs e)
        {
            if (propertyGrid == null)
            {
                return;
            }
                
            propertyGrid.SelectedObject = propertyGridComboBox.SelectedIndex switch
            {
                0 => textEditor,
                1 => textEditor.TextArea,
                2 => textEditor.Options,
                _ => propertyGrid.SelectedObject
            };
        }

        CompletionWindow _completionWindow;

        private void TextEditor_TextArea_TextEntered(object sender, TextCompositionEventArgs e)
        {
            if (e.Text != ".")
            {
                return;
            }

            // open code completion after the user has pressed dot:
            _completionWindow = new CompletionWindow(textEditor.TextArea);
            // provide AvalonEdit with the data:
            var data = _completionWindow.CompletionList.CompletionData;
            data.Add(new MyCompletionData("Item1"));
            data.Add(new MyCompletionData("Item2"));
            data.Add(new MyCompletionData("Item3"));
            data.Add(new MyCompletionData("Another item"));
            _completionWindow.Show();
            _completionWindow.Closed += delegate {
                _completionWindow = null;
            };
        }

        void textEditor_TextArea_TextEntering(object sender, TextCompositionEventArgs e)
        {
            if (e.Text.Length > 0 && _completionWindow != null)
            {
                if (!char.IsLetterOrDigit(e.Text[0]))
                {
                    // Whenever a non-letter is typed while the completion window is open,
                    // insert the currently selected element.
                    _completionWindow.CompletionList.RequestInsertion(e);
                }
            }
            // do not set e.Handled=true - we still want to insert the character that was typed
        }

        #region Folding
        FoldingManager _foldingManager;
        object _foldingStrategy;

        private void HighlightingComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (textEditor.SyntaxHighlighting == null)
            {
                _foldingStrategy = null;
            }
            else
            {
                switch (textEditor.SyntaxHighlighting.Name)
                {
                    case "XML":
                        _foldingStrategy = new XmlFoldingStrategy();
                        textEditor.TextArea.IndentationStrategy = new ICSharpCode.AvalonEdit.Indentation.DefaultIndentationStrategy();
                        break;
                    case "C#":
                    case "C++":
                    case "PHP":
                    case "Java":
                        textEditor.TextArea.IndentationStrategy = new ICSharpCode.AvalonEdit.Indentation.CSharp.CSharpIndentationStrategy(textEditor.Options);
                        _foldingStrategy = new BraceFoldingStrategy();
                        break;
                    default:
                        textEditor.TextArea.IndentationStrategy = new ICSharpCode.AvalonEdit.Indentation.DefaultIndentationStrategy();
                        _foldingStrategy = null;
                        break;
                }
            }
            if (_foldingStrategy != null)
            {
                if (_foldingManager == null)
                    _foldingManager = FoldingManager.Install(textEditor.TextArea);
                UpdateFoldings();
            }
            else
            {
                if (_foldingManager != null)
                {
                    FoldingManager.Uninstall(_foldingManager);
                    _foldingManager = null;
                }
            }
        }

        private void UpdateFoldings()
        {
            switch (_foldingStrategy)
            {
                case BraceFoldingStrategy strategy:
                    strategy.UpdateFoldings(_foldingManager, textEditor.Document);
                    break;

                case XmlFoldingStrategy strategy:
                    strategy.UpdateFoldings(_foldingManager, textEditor.Document);
                    break;
            }
        }
        #endregion
    }
}
