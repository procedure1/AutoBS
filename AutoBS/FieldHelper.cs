using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace AutoBS
{
    public static class FieldHelper
    {
        public static T Get<T>(object obj, string fieldName)
        {
            return (T)obj.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(obj);
        }

        public static bool TryGet<T>(object obj, string fieldName, out T val)
        {
            FieldInfo f = obj.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            if (f == null)
            {
                val = default;
                return false;
            }
            val = (T)f.GetValue(obj);
            return true;
        }

        //The Set method uses reflection to find the specified field within the object's type and set its value.
        //If the field is found (f != null), it sets the value of the field in the provided object to the given value (f.SetValue(obj, value)).
        //If the field is not found, it returns false to indicate that the field could not be set;
        //This technique is useful for accessing and modifying private fields in situations where direct access is not available.
        public static bool Set(object obj, string fieldName, object value)
        {
            FieldInfo f = obj.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            if (f == null)
            {
                Plugin.Log.Error($"FieldHelper.cs Set() - UNABLE to set {fieldName}");
                return false;
            }
                
            f.SetValue(obj, value);
            //Plugin.Log.Info($"FieldHelper.cs Set() - Has set {fieldName} to {value}"); - works on all 9 attributes
            return true;
        }
        public static bool SetPropertyWithPrivateSetter(object obj, string propertyName, object value)
        {
            var propertyInfo = obj.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (propertyInfo == null)
            {
                Plugin.Log.Error($"Unable to find property {propertyName}");
                return false;
            }

            // This is a direct approach to find and invoke the private setter.
            var setter = propertyInfo.GetSetMethod(true);
            if (setter != null)
            {
                setter.Invoke(obj, new[] { value });
                return true;
            }
            else
            {
                Plugin.Log.Error($"Property {propertyName} does not have an accessible setter.");
                return false;
            }
        }
        public static bool SetBackingField(object obj, string fieldName, object value)
        {
            FieldInfo fieldInfo = obj.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            if (fieldInfo != null)
            {
                fieldInfo.SetValue(obj, value);
                return true;
            }
            else
            {
                Plugin.Log.Error($"Unable to find backing field {fieldName}");
                return false;
            }
        }

        public static bool SetProperty(object obj, string propertyName, object value)
        {
            PropertyInfo p = obj.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            if (p == null)
            {
                Plugin.Log.Error($"FieldHelper.cs SetProperty() - UNABLE to set {propertyName}");
                return false;
            }
            p.SetValue(obj, value, null);
            return true;
        }
    }
}
