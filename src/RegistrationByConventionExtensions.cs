﻿using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Builder;
using Unity.Injection;
using Unity.RegistrationByConvention.Exceptions;

namespace Unity.RegistrationByConvention
{
    /// <summary>
    /// Provides a set of convenience overloads to the
    /// <see cref="IUnityContainer"/> interface to support registration of multiple types.
    /// </summary>
    public static class RegistrationByConventionExtensions
    {
        private static readonly Type[] EmptyTypes = new Type[0];

        /// <summary>
        /// Registers the supplied types by using the specified rules for name, lifetime manager, injection members, and registration types.
        /// </summary>
        /// <param name="unityContainer">The container to configure.</param>
        /// <param name="types">The types to register. The methods in the <see cref="AllClasses" /> class can be used to scan assemblies to get types, and further filtering can be performed using LINQ queries.</param>
        /// <param name="getFromTypes">A function that gets the types that will be requested for each type to configure. It can be a method from the <see cref="WithMappings" /> class or a custom function. Defaults to no registration types, and registers only the supplied types.</param>
        /// <param name="getName">A function that gets the name to use for the registration of each type. It can be a method from the <see cref="WithName" /> or a custom function. Defaults to no name.</param>
        /// <param name="getLifetimeManager">A function that gets the <see cref="LifetimeManager" /> for the registration of each type. It can be a method from the <see cref="WithLifetime" /> class or a custom function. Defaults to no lifetime management.</param>
        /// <param name="getInjectionMembers">A function that gets the additional <see cref="IInjectionMember" /> objects for the registration of each type. Defaults to no injection members.</param>
        /// <param name="overwriteExistingMappings"><see langword="true"/> to overwrite existing mappings; otherwise, <see langword="false"/>. Defaults to <see langword="false"/>.</param>
        /// <returns>
        /// The container that this method was called on.
        /// </returns>
        /// <exception cref="ArgumentException">A new registration would overwrite an existing mapping and <paramref name="overwriteExistingMappings"/> is <see langword="false"/>.</exception>
        public static IUnityContainer RegisterTypes(
            this IUnityContainer unityContainer,
            IEnumerable<Type> types,
            Func<Type, IEnumerable<Type>> getFromTypes = null,
            Func<Type, string> getName = null,
            Func<Type, LifetimeManager> getLifetimeManager = null,
            Func<Type, IEnumerable<IInjectionMember>> getInjectionMembers = null,
            bool overwriteExistingMappings = false)
        {
            var container = unityContainer ?? throw new ArgumentNullException(nameof(unityContainer));

            if (getFromTypes == null)
            {
                getFromTypes = t => EmptyTypes;
            }

            if (getName == null)
            {
                getName = t => null;
            }

            if (getLifetimeManager == null)
            {
                getLifetimeManager = t => null;
            }

            if (getInjectionMembers == null)
            {
                getInjectionMembers = t => Enumerable.Empty<IInjectionMember>();
            }

            // capture the existing mappings in a dictionary
            // the collection of registrations in a container is computed on the fly so it should not be queried multiple times
            var mappings =
                !overwriteExistingMappings
                    ? container.Registrations.Where(r => r.RegisteredType != r.MappedToType).ToDictionary(r => new NamedTypeBuildKey(r.RegisteredType, r.Name), r => r.MappedToType)
                    : null;

            foreach (var type in types ?? throw new ArgumentNullException(nameof(types)))
            {
                var fromTypes = getFromTypes(type)?.ToArray() ?? new Type[0];
                var name = getName(type);
                var injectionMembers = getInjectionMembers(type)?.ToArray();

                if (0 < fromTypes.Length)
                {
                    foreach (var fromType in fromTypes.Where(t => t != typeof(IDisposable)))
                    {
                        if (!overwriteExistingMappings)
                        {
                            var key = new NamedTypeBuildKey(fromType, name);
                            if (mappings.TryGetValue(key, out var currentMappedToType) && (type != currentMappedToType))
                            {
                                throw new DuplicateTypeMappingException(name, fromType, currentMappedToType, type);
                            }

                            mappings[key] = type;
                        }

                        container.RegisterType(fromType, type, name, getLifetimeManager(type), injectionMembers);
                    }
                }
                else
                {
                    var lifetimeManager = getLifetimeManager(type);
                    if (null != lifetimeManager || (null != injectionMembers && 0 < injectionMembers.Length))
                        container.RegisterType(null, type, name, lifetimeManager, injectionMembers);
                }
            }

            return container;
        }

        /// <summary>
        /// Registers the types according to the <paramref name="convention"/>.
        /// </summary>
        /// <param name="container">The container to configure.</param>
        /// <param name="convention">The convention to determine which types will be registered and how.</param>
        /// <param name="overwriteExistingMappings"><see langword="true"/> to overwrite existing mappings; otherwise, <see langword="false"/>. Defaults to <see langword="false"/>.</param>
        /// <returns>
        /// The container that this method was called on.
        /// </returns>
        public static IUnityContainer RegisterTypes(this IUnityContainer container, RegistrationConvention convention, bool overwriteExistingMappings = false)
        {
            (container ?? throw new ArgumentNullException(nameof(container)))
                .RegisterTypes((convention ?? throw new ArgumentNullException(nameof(convention))).GetTypes(), 
                               convention.GetFromTypes(), convention.GetName(), convention.GetLifetimeManager(),
                               convention.GetInjectionMembers(), overwriteExistingMappings);

            return container;
        }

        private static void RegisterTypeMappings(IUnityContainer container, bool overwriteExistingMappings, Type type, string name, IEnumerable<Type> fromTypes, Dictionary<NamedTypeBuildKey, Type> mappings)
        {
            foreach (var fromType in fromTypes.Where(t => t != typeof(IDisposable)))
            {
                if (!overwriteExistingMappings)
                {
                    var key = new NamedTypeBuildKey(fromType, name);
                    Type currentMappedToType;
                    if (mappings.TryGetValue(key, out currentMappedToType) && (type != currentMappedToType))
                    {
                        throw new DuplicateTypeMappingException(name, fromType, currentMappedToType, type);
                    }

                    mappings[key] = type;
                }

                container.RegisterType(fromType, type, name, null);
            }
        }
    }
}
