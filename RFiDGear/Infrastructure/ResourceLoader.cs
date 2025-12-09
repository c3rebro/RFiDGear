using Serilog;

using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Resources;
using System.Windows;
using System.Windows.Data;
using System.Windows.Markup;
using System.Diagnostics;
using System.Reflection;

using RFiDGear.Infrastructure.FileAccess;

namespace RFiDGear.Infrastructure
{
    /// <summary>
    /// Description of ResourceLoaderViewModel.
    /// </summary>

    /// <summary>
    /// Enables Binding even if target is not part of visual or logical tree. Thanks to:
    /// https://www.thomaslevesque.com/2011/03/21/wpf-how-to-bind-to-data-when-the-datacontext-is-not-inherited/
    /// </summary>
    public class BindingProxy : Freezable
    {
        #region Overrides of Freezable

        /// <summary>
        ///
        /// </summary>
        /// <returns></returns>
        protected override Freezable CreateInstanceCore()
        {
            return new BindingProxy();
        }

        #endregion Overrides of Freezable

        public object Data
        {
            get => GetValue(DataProperty);
            set => SetValue(DataProperty, value);
        }

        // Using a DependencyProperty as the backing store for Data.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty DataProperty =
            DependencyProperty.Register("Data", typeof(object), typeof(BindingProxy), new UIPropertyMetadata(null));
    }

    /// <summary>
    ///
    /// </summary>
    public sealed class EnumerateExtension : MarkupExtension
    {
        private readonly CultureInfo cultureInfo;
        private readonly ResourceManager resManager;

        /// <summary>
        ///
        /// </summary>
        public Type Type { get; set; }

        /// <summary>
        ///
        /// </summary>
        /// <param name="type"></param>
        public EnumerateExtension(Type type)
        {
            Type = type;

            var projectManager = new ProjectManager();
            var settings = projectManager.LoadSettings();

            resManager = new ResourceManager("RFiDGear.Resources.Manifest", Assembly.GetExecutingAssembly());

            cultureInfo = settings.DefaultSpecification.DefaultLanguage == "german" ? new CultureInfo("de") : new CultureInfo("en");
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="serviceProvider"></param>
        /// <returns></returns>
        public override object ProvideValue(IServiceProvider serviceProvider)
        {
            var names = Enum.GetNames(Type);
            var values = new string[names.Length];

            for (var i = 0; i < names.Length; i++)
            { values[i] = ResourceLoader.GetResource(string.Format("ENUM.{0}.{1}", Type.Name, names[i])); }

            return values;
        }
    }

    /// <summary>
    ///
    /// </summary>
    public sealed class ResourceLoader : IValueConverter, IDisposable
    {
        private readonly ILogger logger = Log.ForContext<ResourceLoader>();
        private readonly ResourceManager resManager;

        /// <summary>
        ///
        /// </summary>
        public ResourceLoader()
        {
            resManager = new ResourceManager("RFiDGear.Resources.Manifest", Assembly.GetExecutingAssembly());
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="value"></param>
        /// <param name="targetType"></param>
        /// <param name="parameter"></param>
        /// <param name="language"></param>
        /// <returns></returns>
        public object Convert(object value, Type targetType, object parameter, CultureInfo language)
        {
            try
            {
                if (parameter is string)
                {
                    return GetResource(parameter as string);
                }
                else if (value != null && value.GetType() == typeof(ObservableCollection<string>))
                {
                    var collection = new ObservableCollection<string>();

                    foreach (var s in value as ObservableCollection<string>)
                    {
                        collection.Add(GetResource(string.Format("ENUM.{0}", s)));
                    }
                    return collection;
                }
                else if (value != null && !(value is string))
                {
                    var t = string.Format("ENUM.{0}.{1}", value.GetType().Name, Enum.GetName(value.GetType(), value));
                    return GetResource(string.Format("ENUM.{0}.{1}", value.GetType().Name, Enum.GetName(value.GetType(), value)));
                }
                else if (value is string)
                {
                    return GetResource(string.Format("ENUM.{0}.{1}", value.GetType().Name, value));
                }
                else
                {
                    return "Ressource not Found";
                }
            }
            catch (Exception e)
            {
                logger.Error(e, "Failed to convert resource {ResourceValue} with parameter {Parameter}", value, parameter);

                throw new ArgumentOutOfRangeException(
                    string.Format("parameter:{0}\nvalue:{1}",
                                  parameter ?? "no param",
                                  value ?? "no value"));
            }
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="value"></param>
        /// <param name="targetType"></param>
        /// <param name="parameter"></param>
        /// <param name="language"></param>
        /// <returns></returns>
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo language)
        {
            if (value != null)
            {
                var names = Enum.GetNames(parameter as Type);
                var values = new string[names.Length];

                for (var i = 0; i < names.Length; i++)
                {
                    values[i] = GetResource(string.Format("ENUM.{0}.{1}", targetType.Name, names[i]));
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
        ///
        /// </summary>
        public void Dispose()
        {
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="resName"></param>
        /// <returns></returns>
        public static string GetResource(string resName)
        {
            try
            {
                var projectManager = new ProjectManager();
                var settingsResult = projectManager.LoadSettings();

                var ressource = new ResourceManager("RFiDGear.Resources.Manifest", Assembly.GetExecutingAssembly())
                    .GetString(resName, settingsResult.DefaultSpecification.DefaultLanguage == "german" ? new CultureInfo("de") : new CultureInfo("en"));

                return ressource.Replace("%NEWLINE", "\n");

            }
            catch (Exception)
            {
                //logger.Error(e, "Failed to resolve resource {ResourceName}", resName);
                return string.Empty;
            }
        }
    }
}