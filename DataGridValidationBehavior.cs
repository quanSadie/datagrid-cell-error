using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;

namespace Demo
{
    public static class DataGridTypeValidationBehavior
    {
        public static readonly DependencyProperty IsEnabledProperty =
            DependencyProperty.RegisterAttached("IsEnabled", typeof(bool), typeof(DataGridTypeValidationBehavior),
                new PropertyMetadata(false, OnIsEnabledChanged));

        public static bool GetIsEnabled(DependencyObject obj) => (bool)obj.GetValue(IsEnabledProperty);
        public static void SetIsEnabled(DependencyObject obj, bool value) => obj.SetValue(IsEnabledProperty, value);

        private static void OnIsEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is DataGrid dataGrid)
            {
                if ((bool)e.NewValue)
                {
                    dataGrid.PreviewLostKeyboardFocus += DataGrid_PreviewLostKeyboardFocus;
                    dataGrid.CellEditEnding += DataGrid_CellEditEnding;
                    dataGrid.LoadingRow += DataGrid_LoadingRow;
                    dataGrid.UnloadingRow += DataGrid_UnloadingRow;
                    dataGrid.PreviewMouseDown += DataGrid_PreviewMouseDown;
                }
                else
                {
                    dataGrid.PreviewLostKeyboardFocus -= DataGrid_PreviewLostKeyboardFocus;
                    dataGrid.CellEditEnding -= DataGrid_CellEditEnding;
                    dataGrid.LoadingRow -= DataGrid_LoadingRow;
                    dataGrid.UnloadingRow -= DataGrid_UnloadingRow;
                    dataGrid.PreviewMouseDown -= DataGrid_PreviewMouseDown;
                }
            }
        }

        private static void DataGrid_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            var dataGrid = (DataGrid)sender;
            if (IsCellInError(dataGrid))
            {
                e.Handled = false;
            }
        }

        private static void DataGrid_UnloadingRow(object sender, DataGridRowEventArgs e)
        {
            e.Row.SetValue(Validation.ErrorTemplateProperty, null);
        }

        private static void DataGrid_LoadingRow(object sender, DataGridRowEventArgs e)
        {
            e.Row.SetValue(Validation.ErrorTemplateProperty, CreateErrorTemplate());
        }

        private static ControlTemplate CreateErrorTemplate()
        {
            const string xaml = @"
        <ControlTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation'>
            <DockPanel LastChildFill='True'>
                <Border DockPanel.Dock='Left'
                        Width='16' Height='16'
                        Margin='0,0,5,0'
                        CornerRadius='8'
                        Background='Red'
                        VerticalAlignment='Center'
                        ToolTipService.ShowOnDisabled='True'>
                    <TextBlock Text='!'
                               Foreground='White'
                               FontWeight='Bold'
                               HorizontalAlignment='Center'
                               VerticalAlignment='Center'/>
                </Border>
                <AdornedElementPlaceholder Name='placeholder'/>
            </DockPanel>
        </ControlTemplate>";

            var template = (ControlTemplate)XamlReader.Parse(xaml);
            return template;
        }

        private static void DataGrid_PreviewLostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            var dataGrid = (DataGrid)sender;
            if (IsCellInError(dataGrid))
            {
                if (e.NewFocus is DependencyObject newFocus && !dataGrid.IsAncestorOf(newFocus))
                {
                    e.Handled = true;
                    ShowErrorTooltip(dataGrid);
                }
            }
        }

        private static void ShowErrorTooltip(DataGrid dataGrid)
        {
            if (dataGrid.CurrentCell.Column?.GetCellContent(dataGrid.CurrentCell.Item) is FrameworkElement element &&
                Validation.GetErrors(element).Count > 0)
            {
                var errorMessage = Validation.GetErrors(element)[0].ErrorContent.ToString();

                // Create a temporary tooltip
                var toolTip = new ToolTip
                {
                    Content = errorMessage,
                    Background = Brushes.LightPink,
                    Foreground = Brushes.DarkRed,
                    Placement = System.Windows.Controls.Primitives.PlacementMode.Right,
                    PlacementTarget = element
                };

                // Set the tooltip directly on the element
                element.ToolTip = toolTip;
                toolTip.IsOpen = true;

                // Auto-close after 3 seconds
                var timer = new System.Windows.Threading.DispatcherTimer
                {
                    Interval = TimeSpan.FromSeconds(3)
                };
                timer.Tick += (s, args) =>
                {
                    toolTip.IsOpen = false;
                    element.ToolTip = null;
                    timer.Stop();
                };
                timer.Start();
            }
        }

        private static void DataGrid_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
        {
            if (e.EditAction == DataGridEditAction.Cancel)
                return;

            var dataGrid = (DataGrid)sender;
            var editingElement = e.EditingElement as FrameworkElement;

            if (editingElement != null && !ValidateCellValue(editingElement, e.Column))
            {
                e.Cancel = true;
                dataGrid.Dispatcher.BeginInvoke(new Action(() =>
                {
                    dataGrid.CurrentCell = new DataGridCellInfo(e.Row.Item, e.Column);
                    dataGrid.BeginEdit();
                    ShowErrorTooltip(dataGrid);
                }));
            }
        }

        private static bool IsCellInError(DataGrid dataGrid)
        {
            if (dataGrid.CurrentCell.IsValid &&
                dataGrid.CurrentCell.Column != null &&
                dataGrid.CurrentCell.Column.GetCellContent(dataGrid.CurrentCell.Item) is FrameworkElement element)
            {
                return Validation.GetHasError(element);
            }
            return false;
        }

        private static bool ValidateCellValue(FrameworkElement editingElement, DataGridColumn column)
        {
            if (column is DataGridTextColumn textColumn)
            {
                var binding = textColumn.Binding as Binding;
                if (binding != null)
                {
                    var propertyType = GetPropertyType(binding.Path.Path, editingElement.DataContext);
                    if (propertyType != null)
                    {
                        try
                        {
                            var value = (editingElement as TextBox)?.Text;
                            if (propertyType == typeof(string))
                            {
                                return true;
                            }
                            else if (propertyType == typeof(int))
                            {
                                int.Parse(value);
                                return true;
                            }
                            else if (propertyType == typeof(double))
                            {
                                double.Parse(value);
                                return true;
                            }
                            else if (propertyType == typeof(decimal))
                            {
                                decimal.Parse(value);
                                return true;
                            }
                            else if (propertyType == typeof(DateTime))
                            {
                                DateTime.Parse(value);
                                return true;
                            }
                            else if (propertyType == typeof(bool))
                            {
                                bool.Parse(value);
                                return true;
                            }
                        }
                        catch (Exception ex)
                        {
                            var bindingExpression = BindingOperations.GetBindingExpression(editingElement, TextBox.TextProperty);
                            if (bindingExpression != null)
                            {
                                var validationError = new ValidationError(
                                    new ExceptionValidationRule(),
                                    bindingExpression,
                                    $"Invalid {propertyType.Name} value: {ex.Message}",
                                    ex);

                                Validation.MarkInvalid(bindingExpression, validationError);
                            }
                            return false;
                        }
                    }
                }
            }
            return true;
        }

        private static Type GetPropertyType(string propertyPath, object dataContext)
        {
            if (dataContext == null || string.IsNullOrEmpty(propertyPath))
                return null;

            var propertyInfo = dataContext.GetType().GetProperty(propertyPath);
            return propertyInfo?.PropertyType;
        }
    }
}