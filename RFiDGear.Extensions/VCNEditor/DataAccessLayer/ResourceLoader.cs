
using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Resources;
using System.IO;
using System.Windows;
using System.Windows.Data;
using System.Windows.Markup;

namespace RFiDGear.Extensions.VCNEditor.DataAccessLayer
{
    /// <summary>
    /// Provides a shared culture override for resource lookups in the VCN editor.
    /// </summary>
    public static class CultureInfoProxy
    {
        /// <summary>
        /// Gets or sets the culture used for localized resource lookups.
        /// </summary>
        public static CultureInfo Culture { get; set; }
    }

    /// <summary>
    /// Enables Binding even if target is not part of visual or logical tree. Thanks to:
    /// https://www.thomaslevesque.com/2011/03/21/wpf-how-to-bind-to-data-when-the-datacontext-is-not-inherited/
    /// </summary>
    public class BindingProxy : Freezable
    {
        #region Overrides of Freezable

        /// <summary>
        /// Creates a new instance of the binding proxy for WPF cloning.
        /// </summary>
        /// <returns>A new <see cref="BindingProxy"/> instance.</returns>
        protected override Freezable CreateInstanceCore()
        {
            return new BindingProxy();
        }

        #endregion Overrides of Freezable

        /// <summary>
        /// Gets or sets the data object to expose for binding.
        /// </summary>
        public object Data
        {
            get => (object)GetValue(DataProperty);
            set => SetValue(DataProperty, value);
        }

        // Using a DependencyProperty as the backing store for Data.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty DataProperty =
            DependencyProperty.Register("Data", typeof(object), typeof(BindingProxy), new UIPropertyMetadata(null));
    }

    /// <summary>
    /// Exposes localized enum values for use in XAML bindings.
    /// </summary>
    public sealed class EnumerateExtension : MarkupExtension
    {
        private readonly CultureInfo cultureInfo;
        private readonly ResourceManager resManager;

        /// <summary>
        /// Gets or sets the enum type to enumerate.
        /// </summary>
        public Type Type { get; set; }

        /// <summary>
        /// Initializes the extension with the enum type to enumerate.
        /// </summary>
        /// <param name="type">The enum type whose values should be localized.</param>
        public EnumerateExtension(Type type)
        {
            Type = type;
            resManager = new ResourceManager(@"RFiDGear.Extensions.VCNEditor.Resources.Manifest", typeof(RFiDGear.Extensions.VCNEditor.ViewModel.VCNEditorViewModel).Assembly);

            cultureInfo = CultureInfoProxy.Culture ?? CultureInfo.CurrentUICulture;
        }

        /// <summary>
        /// Builds the localized enum value array for XAML usage.
        /// </summary>
        /// <param name="serviceProvider">The service provider for markup extensions.</param>
        /// <returns>The array of localized enum display strings.</returns>
        public override object ProvideValue(IServiceProvider serviceProvider)
        {
            string[] names = Enum.GetNames(Type);
            string[] values = new string[names.Length];

            for (int i = 0; i < names.Length; i++)
            { values[i] = ResourceLoader.getResource(string.Format("ENUM.{0}.{1}", Type.Name, names[i])); }

            return values;
        }
    }

    /// <summary>
    /// Converts enum values and keys into localized resource strings.
    /// </summary>
    public sealed class ResourceLoader : IValueConverter, IDisposable
    {
        private readonly CultureInfo cultureInfo;
        private readonly ResourceManager resManager;

        /// <summary>
        /// Initializes a new resource loader using the current UI culture.
        /// </summary>
        public ResourceLoader()
        {
            resManager = new ResourceManager(@"RFiDGear.Extensions.VCNEditor.Resources.Manifest", typeof(RFiDGear.Extensions.VCNEditor.ViewModel.VCNEditorViewModel).Assembly);
            cultureInfo = CultureInfoProxy.Culture ?? CultureInfo.CurrentUICulture;
        }

        /// <summary>
        /// Converts enum values or resource keys into localized strings.
        /// </summary>
        /// <param name="value">The value to convert, such as an enum or string key.</param>
        /// <param name="targetType">The expected target type.</param>
        /// <param name="parameter">Optional key parameter for lookups.</param>
        /// <param name="language">The language to use for conversion.</param>
        /// <returns>The localized string or localized collection of strings.</returns>
        public object Convert(object value, Type targetType, object parameter, CultureInfo language)
        {
            try
            {
                if (parameter is string key)
                {
                    return ResourceLoader.getResource(key);
                }
                else if (value is ObservableCollection<string> collection)
                {
                    var localizedCollection = new ObservableCollection<string>();

                    foreach (string s in collection)
                    {
                        localizedCollection.Add(ResourceLoader.getResource(string.Format("ENUM.{0}", s)));
                    }
                    return localizedCollection;
                }
                else if (value != null && !(value is string))
                {
                    string resourceKey = string.Format("ENUM.{0}.{1}", value.GetType().Name, Enum.GetName(value.GetType(), value));
                    return ResourceLoader.getResource(resourceKey);
                }
                else if (value is string stringValue)
                {
                    return ResourceLoader.getResource(string.Format("ENUM.{0}.{1}", value.GetType().Name, stringValue));
                }
                else
                {
                    return "Ressource not Found";
                }
            }
            catch (Exception e)
            {
                LogWriter.CreateLogEntry(string.Format("{0}; {1}; {2}", DateTime.Now, e.Message, e.InnerException != null ? e.InnerException.Message : ""));

                string errorMessage = string.Format("Resource conversion failed. parameter:{0}\nvalue:{1}",
                    parameter ?? "no param",
                    value ?? "no value");
                throw new InvalidOperationException(errorMessage, e);
            }
        }

        /// <summary>
        /// Converts a localized enum display string back into the enum name.
        /// </summary>
        /// <param name="value">The localized value selected by the user.</param>
        /// <param name="targetType">The target enum type.</param>
        /// <param name="parameter">The enum type parameter for conversion.</param>
        /// <param name="language">The language to use for conversion.</param>
        /// <returns>The enum name matching the localized value.</returns>
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo language)
        {
            if (value != null)
            {
                string[] names = Enum.GetNames(parameter as Type);
                string[] values = new string[names.Length];

                for (int i = 0; i < names.Length; i++)
                {
                    values[i] = ResourceLoader.getResource(string.Format("ENUM.{0}.{1}", targetType.Name, names[i]));
                    if ((string)value == values[i])
                    {
                        return names[i];
                    }
                }

                throw new ArgumentException(null, "value");
            }

            return null;
        }

        /// <summary>
        /// Disposes of the resource loader.
        /// </summary>
        public void Dispose()
        {
        }

        /// <summary>
        /// Looks up a localized resource string by key.
        /// </summary>
        /// <param name="resName">The resource key to retrieve.</param>
        /// <returns>The localized resource string, or an empty string if not found.</returns>
        public static string getResource(string resName)
        {

            try
            {
                System.Reflection.Assembly ass = typeof(RFiDGear.Extensions.VCNEditor.ViewModel.VCNEditorViewModel).Assembly;

                return new ResourceManager(@"RFiDGear.Extensions.VCNEditor.Resources.Manifest", ass)
                    .GetString(resName, CultureInfoProxy.Culture ?? CultureInfo.CurrentUICulture);

            }
            catch (Exception e)
            {
                LogWriter.CreateLogEntry(string.Format("{0}; {1}; {2}", DateTime.Now, e.Message, e.InnerException != null ? e.InnerException.Message : ""));
                return string.Empty;
            }
        }
    }
}
