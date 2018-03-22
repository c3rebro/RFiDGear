using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.Composition;
using System.ComponentModel.Composition.Hosting;
using System.Dynamic;
using System.Linq;
using System.Text;
using GalaSoft.MvvmLight;

namespace MefMvvm.SharedContracts.ViewModel
{
    [TypeDescriptionProvider(typeof(ModelViewMapDescriptionProvider))]
    public class ViewModelLocator : DynamicObject, ITypedList
    {
        #region Properties
        [ImportMany("ViewModel", AllowRecomposition = true)]
        private IEnumerable<Lazy<object, IViewModelMetadata>> ViewModels { get; set; }
        private static Dictionary<string, object> dictionary = new Dictionary<string, object>();
        #endregion

        #region DynamicObject
        public int Count { get { return dictionary.Count; } }

        public override bool TryGetMember(GetMemberBinder binder, out object result)
        {
            string name = binder.Name;
            if (!dictionary.TryGetValue(name, out result))
                try
                {
                    if (ViewModels == null)
                    {
                        MefHelper.Instance.Compose();
                        MefHelper.Instance.Container.ComposeParts(this);
                    }

                    dictionary[binder.Name] = (result = ViewModels.Single(v => v.Metadata.Name.Equals(name)).Value);
                    return result != null;
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex);
                }
            return true;
        }

        public override bool TrySetMember(SetMemberBinder binder, object value)
        {
            dictionary[binder.Name] = value;
            return true;
        }
        #endregion

        #region ITypedList implementation
        public PropertyDescriptorCollection GetItemProperties(PropertyDescriptor[] listAccessors)
        {
            var result = new PropertyDescriptorCollection(null);
            foreach (var m in dictionary)
                result.Add(new ModelViewPropertyDescriptor(m.Key, m.Value));
            return result;
        }

        public string GetListName(PropertyDescriptor[] listAccessors)
        {
            return "Models";
        }
        #endregion

        #region ModelViewPropertyDescriptor
        /// <summary>
        /// A property descriptor which exposes an ICommand instance
        /// </summary>
        internal class ModelViewPropertyDescriptor : PropertyDescriptor
        {
            internal object ModelView { get; set; }

            /// <summary>
            /// Construct the descriptor
            /// </summary>
            /// <param name="command"></param>
            public ModelViewPropertyDescriptor(string name, object modelView)
                : base(name, null)
            {
                ModelView = modelView;
            }

            /// <summary>
            /// Always read only in this case
            /// </summary>
            public override bool IsReadOnly
            {
                get { return true; }
            }

            /// <summary>
            /// Nope, it's read only
            /// </summary>
            /// <param name="component"></param>
            /// <returns></returns>
            public override bool CanResetValue(object component)
            {
                return false;
            }

            /// <summary>
            /// Not needed
            /// </summary>
            public override Type ComponentType
            {
                get { return ModelView.GetType(); }
            }

            /// <summary>
            /// Get the ICommand from the parent command map
            /// </summary>
            /// <param name="component"></param>
            /// <returns></returns>
            public override object GetValue(object component)
            {
                return ModelView;
            }

            /// <summary>
            /// Get the type of the property
            /// </summary>
            public override Type PropertyType
            {
                get { return ModelView.GetType(); }
            }

            /// <summary>
            /// Not needed
            /// </summary>
            /// <param name="component"></param>
            public override void ResetValue(object component)
            {
                throw new NotImplementedException();
            }

            /// <summary>
            /// Not needed
            /// </summary>
            /// <param name="component"></param>
            /// <param name="value"></param>
            public override void SetValue(object component, object value)
            {
                throw new NotImplementedException();
            }

            /// <summary>
            /// Not needed
            /// </summary>
            /// <param name="component"></param>
            /// <returns></returns>
            public override bool ShouldSerializeValue(object component)
            {
                return true;
            }

            public override string ToString()
            {
                return this.Name;
            }
        }
        #endregion

        #region ModelViewMapDescriptionProvider
        /// <summary>
        /// Expose the dictionary entries of a CommandMap as properties
        /// </summary>
        private class ModelViewMapDescriptionProvider : TypeDescriptionProvider
        {
            /// <summary>
            /// Standard constructor
            /// </summary>
            public ModelViewMapDescriptionProvider()
                : this(TypeDescriptor.GetProvider(typeof(ViewModelLocator)))
            {
            }

            /// <summary>
            /// Construct the provider based on a parent provider
            /// </summary>
            /// <param name="parent"></param>
            public ModelViewMapDescriptionProvider(TypeDescriptionProvider parent)
                : base(parent)
            {
            }

            /// <summary>
            /// Get the type descriptor for a given object instance
            /// </summary>
            /// <param name="objectType">The type of object for which a type descriptor is requested</param>
            /// <param name="instance">The instance of the object</param>
            /// <returns>A custom type descriptor</returns>
            public override ICustomTypeDescriptor GetTypeDescriptor(Type objectType, object instance)
            {
                return new ModelViewDescriptor(base.GetTypeDescriptor(objectType, instance));
            }
        }
        #endregion

        #region ModelViewDescriptor
        /// <summary>
        /// This class is responsible for providing custom properties to WPF - in this instance
        /// allowing you to bind to commands by name
        /// </summary>
        private class ModelViewDescriptor : CustomTypeDescriptor
        {
            /// <summary>
            /// Store the command map for later
            /// </summary>
            /// <param name="descriptor"></param>
            /// <param name="map"></param>
            public ModelViewDescriptor(ICustomTypeDescriptor descriptor)
                : base(descriptor)
            {
            }

            /// <summary>
            /// Get the properties for this command map
            /// </summary>
            /// <returns>A collection of synthesized property descriptors</returns>
            public override PropertyDescriptorCollection GetProperties()
            {
                var result = new PropertyDescriptorCollection(null);
                foreach (var m in dictionary)
                    result.Add(new ModelViewPropertyDescriptor(m.Key, m.Value));
                return result;
            }
        }

        #endregion

        public static void Cleanup()
        {
            foreach (var item in dictionary)
            {
                if (item.Value != null)
                {
                    var vm = item.Value as ViewModelBase;
                    vm.Cleanup();
                }
            }
        }
    }
}
