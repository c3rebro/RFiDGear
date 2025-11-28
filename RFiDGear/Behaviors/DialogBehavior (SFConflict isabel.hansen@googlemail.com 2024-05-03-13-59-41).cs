using MvvmDialogs.ViewModels;
using MvvmDialogs.Presenters;

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Reflection;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace MvvmDialogs.Behaviors
{
    public static class DialogBehavior
    {
        private static readonly Dictionary<IDialogViewModel, Window> DialogBoxes = new Dictionary<IDialogViewModel, Window>();
        private static readonly Dictionary<UserControl, NotifyCollectionChangedEventHandler> UserControlChangeNotificationHandlers = new Dictionary<UserControl, NotifyCollectionChangedEventHandler>();
        private static readonly Dictionary<Window, NotifyCollectionChangedEventHandler> ChangeNotificationHandlers = new Dictionary<Window, NotifyCollectionChangedEventHandler>();
        private static readonly Dictionary<ObservableCollection<IDialogViewModel>, List<IDialogViewModel>> DialogBoxViewModels = new Dictionary<ObservableCollection<IDialogViewModel>, List<IDialogViewModel>>();
        private static ResourceDictionary resourceDictionary;

        public static readonly DependencyProperty ClosingProperty = DependencyProperty.RegisterAttached(
            "Closing",
            typeof(bool),
            typeof(DialogBehavior),
            new PropertyMetadata(false));

        public static readonly DependencyProperty ClosedProperty = DependencyProperty.RegisterAttached(
            "Closed",
            typeof(bool),
            typeof(DialogBehavior),
            new PropertyMetadata(false));

        public static readonly DependencyProperty DialogViewModelsProperty = DependencyProperty.RegisterAttached(
            "DialogViewModels",
            typeof(object),
            typeof(DialogBehavior),
            new PropertyMetadata(null, OnDialogViewModelsChange));

        public static void SetDialogViewModels(DependencyObject source, object value)
        {
            source.SetValue(DialogViewModelsProperty, value);
        }

        public static object GetDialogViewModels(DependencyObject source)
        {
            return source.GetValue(DialogViewModelsProperty);
        }

        public static void SetResourceDictionary(string source)
        {
            resourceDictionary = new ResourceDictionary
            {
                Source =
                new Uri(source,
                        UriKind.RelativeOrAbsolute)
            };
        }

        private static void OnDialogViewModelsChange(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is Window)
            {
                // when the parent closes we don't need to track it anymore
                (d as Window).Closed += (s, a) => ChangeNotificationHandlers.Remove(d as Window);

                // otherwise create a handler for it that responds to changes to the supplied collection
                if (!ChangeNotificationHandlers.ContainsKey(d as Window))
                {
                    ChangeNotificationHandlers[d as Window] = (sender, args) =>
                    {
                        if (sender is ObservableCollection<IDialogViewModel> collection)
                        {
                            if (args.Action == NotifyCollectionChangedAction.Add ||
                                args.Action == NotifyCollectionChangedAction.Remove ||
                                args.Action == NotifyCollectionChangedAction.Replace)
                            {
                                if (args.NewItems != null)
                                {
                                    foreach (IDialogViewModel viewModel in args.NewItems)
                                    {
                                        if (!DialogBoxViewModels.ContainsKey(collection))
                                        {
                                            DialogBoxViewModels[collection] = new List<IDialogViewModel>();
                                        }

                                        DialogBoxViewModels[collection].Add(viewModel);
                                        AddDialog(viewModel, collection, d as Window);
                                    }
                                }

                                if (args.OldItems != null)
                                {
                                    foreach (IDialogViewModel viewModel in args.OldItems)
                                    {
                                        RemoveDialog(viewModel);
                                        DialogBoxViewModels[collection].Remove(viewModel);
                                        if (DialogBoxViewModels[collection].Count == 0)
                                        {
                                            DialogBoxViewModels.Remove(collection);
                                        }
                                    }
                                }
                            }
                            else if (args.Action == NotifyCollectionChangedAction.Reset)
                            {
                                // a Reset event is typically generated in response to clearing the collection.
                                // unfortunately the framework doesn't provide us with the list of items being
                                // removed which is why we have to keep a mirror in DialogBoxViewModels
                                if (DialogBoxViewModels.ContainsKey(collection))
                                {
                                    var viewModels = DialogBoxViewModels[collection];
                                    foreach (var viewModel in DialogBoxViewModels[collection])
                                    {
                                        RemoveDialog(viewModel);
                                    }

                                    DialogBoxViewModels.Remove(collection);
                                }
                            }
                        }
                    };
                }

                // when the collection is first bound to this property we should create any initial
                // dialogs the user may have added in the main view model's constructor
                if (e.NewValue is ObservableCollection<IDialogViewModel> newCollection)
                {
                    newCollection.CollectionChanged += ChangeNotificationHandlers[d as Window];
                    foreach (IDialogViewModel viewModel in newCollection.ToList())
                    {
                        AddDialog(viewModel, newCollection, d as Window);
                    }
                }

                // when we remove the binding we need to shut down any dialogs that have been left open
                if (e.OldValue is ObservableCollection<IDialogViewModel> oldCollection)
                {
                    oldCollection.CollectionChanged -= ChangeNotificationHandlers[d as Window];
                    foreach (IDialogViewModel viewModel in oldCollection.ToList())
                    {
                        RemoveDialog(viewModel);
                    }
                }
            }

            else if (d is UserControl)
            {
                // when the parent closes we don't need to track it anymore
                // otherwise create a handler for it that responds to changes to the supplied collection
                if (!UserControlChangeNotificationHandlers.ContainsKey(d as UserControl))
                {
                    UserControlChangeNotificationHandlers[d as UserControl] = (sender, args) =>
                    {
                        if (sender is ObservableCollection<IDialogViewModel> collection)
                        {
                            if (args.Action == NotifyCollectionChangedAction.Add ||
                                args.Action == NotifyCollectionChangedAction.Remove ||
                                args.Action == NotifyCollectionChangedAction.Replace)
                                {
                                    if (args.NewItems != null)
                                    {
                                        foreach (IDialogViewModel viewModel in args.NewItems)
                                        {
                                            if (!DialogBoxViewModels.ContainsKey(collection))
                                            {
                                                DialogBoxViewModels[collection] = new List<IDialogViewModel>();
                                            }

                                            DialogBoxViewModels[collection].Add(viewModel);
                                            AddDialog(viewModel, collection, d as UserControl);
                                        }
                                    }

                                    if (args.OldItems != null)
                                    {
                                        foreach (IDialogViewModel viewModel in args.OldItems)
                                        {
                                            RemoveDialog(viewModel);
                                            DialogBoxViewModels[collection].Remove(viewModel);
                                            if (DialogBoxViewModels[collection].Count == 0)
                                            {
                                                DialogBoxViewModels.Remove(collection);
                                            }
                                        }
                                    }
                                }
                            else if (args.Action == NotifyCollectionChangedAction.Reset)
                            {
                                // a Reset event is typically generated in response to clearing the collection.
                                // unfortunately the framework doesn't provide us with the list of items being
                                // removed which is why we have to keep a mirror in DialogBoxViewModels
                                if (DialogBoxViewModels.ContainsKey(collection))
                                {
                                    var viewModels = DialogBoxViewModels[collection];
                                    foreach (var viewModel in DialogBoxViewModels[collection])
                                    {
                                        RemoveDialog(viewModel);
                                    }

                                    DialogBoxViewModels.Remove(collection);
                                }
                            }
                        }
                    };
                }

                // when the collection is first bound to this property we should create any initial
                // dialogs the user may have added in the main view model's constructor
                if (e.NewValue is ObservableCollection<IDialogViewModel> newCollection)
                {
                    newCollection.CollectionChanged += UserControlChangeNotificationHandlers[d as UserControl];
                    foreach (IDialogViewModel viewModel in newCollection.ToList())
                    {
                        AddDialog(viewModel, newCollection, d as Window);
                    }
                }

                // when we remove the binding we need to shut down any dialogs that have been left open
                if (e.OldValue is ObservableCollection<IDialogViewModel> oldCollection)
                {
                    oldCollection.CollectionChanged -= UserControlChangeNotificationHandlers[d as UserControl];
                    foreach (IDialogViewModel viewModel in oldCollection.ToList())
                    {
                        RemoveDialog(viewModel);
                    }
                }
            }

            else
            {
                return;
            }
        }

        private static void AddDialog(IDialogViewModel viewModel, ObservableCollection<IDialogViewModel> collection, object owner)
        {
            try
            {
                // find the global resource that has been keyed to this view model type
                object resourceFromApplication = Application.Current.TryFindResource(viewModel.GetType());
                object resoruceFromExternalDictionary = resourceDictionary?[viewModel.GetType()];

                var resource = resourceFromApplication ?? resoruceFromExternalDictionary;

                if (resource == null)
                {
                    return;
                }

                // is this resource a presenter?
                if (IsAssignableToGenericType(resource.GetType(), typeof(IDialogBoxPresenter<>)))
                {
                    resource.GetType().GetMethod("Show").Invoke(resource, new object[] { viewModel });
                    collection.Remove(viewModel);
                }

                // is this resource a dialog box window on a window owner?
                else if (resource is Window)
                {
                    if (!(viewModel is IUserDialogViewModel userViewModel))
                    {
                        return;
                    }

                    var dialog = resource as Window;
                    dialog.DataContext = userViewModel;
                    DialogBoxes[userViewModel] = dialog;
                    userViewModel.DialogClosing += (sender, args) =>
                        collection.Remove(sender as IUserDialogViewModel);

                    dialog.Closing += (sender, args) =>
                    {
                        if (!(bool)dialog.GetValue(ClosingProperty))
                        {
                            dialog.SetValue(ClosingProperty, true);
                            userViewModel.RequestClose();
                            if (!(bool)dialog.GetValue(ClosedProperty))
                            {
                                args.Cancel = true;
                                dialog.SetValue(ClosingProperty, false);
                            }
                        }
                    };
                    dialog.Closed += (sender, args) =>
                    {
                        Debug.Assert(DialogBoxes.ContainsKey(userViewModel));
                        DialogBoxes.Remove(userViewModel);
                        return;
                    };

                    dialog.Owner = (owner as Window);

                    if (userViewModel.IsModal)
                    {
                        dialog.ShowDialog();
                    }
                    else
                    {
                        dialog.Show();
                    }
                }
            }

            catch
            {

            }

        }

        private static void RemoveDialog(IDialogViewModel viewModel)
        {
            if (DialogBoxes.ContainsKey(viewModel))
            {
                var dialog = DialogBoxes[viewModel];
                if (!(bool)dialog.GetValue(ClosingProperty))
                {
                    dialog.SetValue(ClosingProperty, true);
                    DialogBoxes[viewModel].Close();
                }
                dialog.SetValue(ClosedProperty, true);
            }
        }

        // courtesy James Fraumeni/StackOverflow: http://stackoverflow.com/questions/74616/how-to-detect-if-type-is-another-generic-type/1075059#1075059
        private static bool IsAssignableToGenericType(Type givenType, Type genericType)
        {
            var interfaceTypes = givenType.GetInterfaces();

            foreach (var it in interfaceTypes)
            {
                if (it.IsGenericType && it.GetGenericTypeDefinition() == genericType)
                {
                    return true;
                }
            }

            if (givenType.IsGenericType && givenType.GetGenericTypeDefinition() == genericType)
            {
                return true;
            }

            Type baseType = givenType.BaseType;
            if (baseType == null)
            {
                return false;
            }

            return IsAssignableToGenericType(baseType, genericType);
        }
    }
}