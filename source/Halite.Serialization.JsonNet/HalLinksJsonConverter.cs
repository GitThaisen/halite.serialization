﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Halite.Serialization.JsonNet
{
    public class HalLinksJsonConverter : JsonConverter
    {
        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            var objectType = value.GetType();

            try
            {
                DoWriteJson(writer, value, serializer, objectType);
            }
            catch (Exception ex)
            {
                throw new JsonWriterException($"Failed to serialize object of type {objectType}!", ex);
            }
        }

        private static void DoWriteJson(JsonWriter writer, object value, JsonSerializer serializer, Type objectType)
        {
            var jo = new JObject();

            var properties = objectType.GetInheritanceChain().Reverse().SelectMany(it => it.GetImmediateProperties()).ToList();
            foreach (var prop in properties.Where(p => p.CanRead))
            {
                var propVal = prop.GetValue(value, null);
                if (propVal != null)
                {
                    jo.Add(prop.GetRelationName(serializer), JToken.FromObject(propVal, serializer));
                }
            }

            jo.WriteTo(writer);
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            if (reader.TokenType == JsonToken.StartObject)
            {
                var jo = JObject.Load(reader);

                var ctor = SelectConstructor(objectType);
                if (ctor == null)
                {
                    throw CreateConstructorException(objectType);
                }

                var instance = CreateInstance(objectType, ctor, jo, serializer);

                AssignValues(objectType, instance, jo);

                return instance;
            }

            throw new InvalidOperationException();
        }

        private static void AssignValues(Type objectType, HalLinks instance, JObject jo)
        {
            var properties = objectType.GetProperties().Where(p => p.SetMethod != null && p.GetMethod != null).ToList();

            foreach (var prop in properties)
            {
                var jop = jo.Properties().FirstOrDefault(it =>
                    string.Equals(it.Name, prop.Name, StringComparison.InvariantCultureIgnoreCase));
                if (jop != null)
                {
                    var currentValue = prop.GetMethod.Invoke(instance, new object[0]);
                    if (currentValue == null)
                    {
                        var jvalue = (JValue)jop.Value;
                        var objValue = jvalue.Value;
                        var value = typeof(Uri) == prop.PropertyType
                            ? new Uri((string)objValue, UriKind.RelativeOrAbsolute)
                            : objValue;
                        prop.SetMethod.Invoke(instance, new[] { value });
                    }
                }
            }
        }

        private static HalLinks CreateInstance(Type objectType, ConstructorInfo ctor, JObject item, JsonSerializer serializer)
        {
            var args = ctor.GetParameters().Select(p => LookupArgument(objectType, p, item, serializer)).ToArray();
            try
            {
                return (HalLinks)ctor.Invoke(args);
            }
            catch (TargetInvocationException ex)
            {
                if (ex.InnerException != null)
                {
                    throw ex.InnerException;
                }

                throw;
            }
        }

        private static object LookupArgument(Type objectType, ParameterInfo parameter, JObject item, JsonSerializer serializer)
        {
            var prop = FindCorrespondingProperty(objectType, parameter.Name);
            if (prop == null)
            {
                throw CreateConstructorException(objectType);
            }

            var relationName = prop.GetRelationName(serializer);
            var documentProperties = item.Properties().ToList();
            var jprop = documentProperties.FirstOrDefault(it => string.Equals(relationName, it.Name, StringComparison.InvariantCultureIgnoreCase));

            if (jprop == null)
            {
                return null;
            }

            var val = jprop.Value.ToObject(parameter.ParameterType, serializer);
            return val;
        }

        private static PropertyInfo FindCorrespondingProperty(Type objectType, string parameterName)
        {
            return objectType.GetProperties().FirstOrDefault(it =>
                string.Equals(it.Name, parameterName, StringComparison.InvariantCultureIgnoreCase));
        }

        private static JsonSerializationException CreateConstructorException(Type objectType)
        {
            return new JsonSerializationException($"Unable to find a constructor to use for type {objectType}. A class should either have a default constructor, one constructor with arguments or a constructor marked with the JsonConstructor attribute.");
        }

        private static ConstructorInfo SelectConstructor(Type objectType)
        {
            var constructors = objectType.GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

            return SelectAnnotatedJsonConstructor(constructors) ??
                   SelectDefaultConstructor(constructors) ??
                   SelectConstructorWithParameters(constructors);
        }

        private static ConstructorInfo SelectAnnotatedJsonConstructor(IReadOnlyList<ConstructorInfo> constructors)
        {
            return constructors.SingleOrDefault(ctor => ctor.GetCustomAttributes(typeof(JsonConstructorAttribute), false).Any());
        }

        private static ConstructorInfo SelectDefaultConstructor(IReadOnlyList<ConstructorInfo> constructors)
        {
            return constructors.SingleOrDefault(ctor => !ctor.GetParameters().Any());
        }

        private static ConstructorInfo SelectConstructorWithParameters(IReadOnlyList<ConstructorInfo> ctors)
        {
            return ctors.Count == 1 ? ctors[0] : null;
        }

        public override bool CanConvert(Type objectType)
        {
            var result = typeof(HalLinks).IsAssignableFrom(objectType);
            return result;
        }
    }
}