﻿using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Data.Edm;
using Microsoft.Data.Edm.Library.Values;

#pragma warning disable 1591

namespace Simple.OData.Client.V3.Adapter
{
    public class Metadata : MetadataBase
    {
        private readonly ISession _session;
        private readonly IEdmModel _model;

        public Metadata(ISession session, IEdmModel model)
        {
            _session = session;
            _model = model;
        }

        public override ISession Session { get { return _session; } }

        public override string GetEntityCollectionExactName(string collectionName)
        {
            IEdmEntitySet entitySet;
            IEdmEntityType entityType;
            if (TryGetEntitySet(collectionName, out entitySet))
            {
                return entitySet.Name;
            }
            else if (TryGetEntityType(collectionName, out entityType))
            {
                return entityType.Name;
            }

            throw new UnresolvableObjectException(collectionName, string.Format("Entity collection [{0}] not found", collectionName));
        }

        public override bool EntityCollectionRequiresOptimisticConcurrencyCheck(string collectionName)
        {
            return GetEntityType(collectionName).StructuralProperties()
                .Any(x => x.ConcurrencyMode == EdmConcurrencyMode.Fixed);
        }

        public override string GetDerivedEntityTypeExactName(string collectionName, string entityTypeName)
        {
            IEdmEntitySet entitySet;
            IEdmEntityType entityType;
            if (TryGetEntitySet(collectionName, out entitySet))
            {
                entityType = (_model.FindAllDerivedTypes(entitySet.ElementType)
                    .BestMatch(x => (x as IEdmEntityType).Name, entityTypeName, _session.Settings.NameMatchResolver) as IEdmEntityType);
                if (entityType != null)
                    return entityType.Name;
            }
            else if (TryGetEntityType(entityTypeName, out entityType))
            {
                return entityType.Name;
            }

            throw new UnresolvableObjectException(entityTypeName, string.Format("Entity type [{0}] not found", entityTypeName));
        }

        public override string GetEntityTypeExactName(string collectionName)
        {
            var entityType = GetEntityTypes().BestMatch(x => x.Name, collectionName, _session.Settings.NameMatchResolver);
            if (entityType != null)
                return entityType.Name;

            throw new UnresolvableObjectException(collectionName, string.Format("Entity type [{0}] not found", collectionName));
        }

        public override string GetLinkedCollectionName(string instanceTypeName, string typeName, out bool isSingleton)
        {
            isSingleton = false;
            IEdmEntitySet entitySet;
            IEdmEntityType entityType;

            if (TryGetEntitySet(instanceTypeName, out entitySet))
                return entitySet.Name;
            if (TryGetEntitySet(typeName, out entitySet))
                return entitySet.Name;
            if (TryGetEntityType(instanceTypeName, out entityType))
                return entityType.Name;
            if (TryGetEntityType(typeName, out entityType))
                return entityType.Name;

            throw new UnresolvableObjectException(typeName, string.Format("Linked collection for type [{0}] not found", typeName));
        }

        public override string GetQualifiedTypeName(string typeName)
        {
            IEdmEntityType entityType;
            if (TryGetEntityType(typeName, out entityType))
            {
                return string.Join(".", entityType.Namespace, entityType.Name);
            }
            IEdmComplexType complexType;
            if (TryGetComplexType(typeName, out complexType))
            {
                return string.Join(".", complexType.Namespace, complexType.Name);
            }
            IEdmEnumType enumType;
            if (TryGetEnumType(typeName, out enumType))
            {
                return string.Join(".", enumType.Namespace, enumType.Name);
            }

            throw new UnresolvableObjectException(typeName, string.Format("Type [{0}] not found", typeName));
        }

        public override bool IsOpenType(string collectionName)
        {
            return GetEntityType(collectionName).IsOpen;
        }

        public override bool IsTypeWithId(string collectionName)
        {
            if (TryGetEntityType(collectionName, out var entityType))
                return entityType.DeclaredKey != null;
            else
                return false;
        }

        public override IEnumerable<string> GetStructuralPropertyNames(string collectionName)
        {
            return GetEntityType(collectionName).StructuralProperties().Select(x => x.Name);
        }

        public override bool HasStructuralProperty(string collectionName, string propertyName)
        {
            return GetEntityType(collectionName).StructuralProperties().Any(x => _session.Settings.NameMatchResolver.IsMatch(x.Name, propertyName));
        }

        public override string GetStructuralPropertyExactName(string collectionName, string propertyName)
        {
            return GetStructuralProperty(collectionName, propertyName).Name;
        }

        public override string GetStructuralPropertyPath(string collectionName, params string[] propertyNames)
        {
            if (propertyNames == null || propertyNames.Length == 0)
                throw new ArgumentNullException(nameof(propertyNames));
            var property = GetStructuralProperty(collectionName, propertyNames[0]);
            var exactNames = new List<string>();
            exactNames.Add(property.Name);

            for (int i = 1; i < propertyNames.Length; i++)
            {
                var entityType = GetComplexType(property.Type.FullName());
                property = GetStructuralProperty(entityType, propertyNames[i]);
                exactNames.Add(property.Name);
                
                if (property.Type.IsPrimitive())
                    break;
            }
            return String.Join("/", exactNames.ToArray());
        }

        public override bool HasNavigationProperty(string collectionName, string propertyName)
        {
            return GetEntityType(collectionName).NavigationProperties().Any(x => _session.Settings.NameMatchResolver.IsMatch(x.Name, propertyName));
        }

        public override string GetNavigationPropertyExactName(string collectionName, string propertyName)
        {
            return GetNavigationProperty(collectionName, propertyName).Name;
        }

        public override string GetNavigationPropertyPartnerTypeName(string collectionName, string propertyName)
        {
            var navigationProperty = GetNavigationProperty(collectionName, propertyName);
            IEdmEntityType entityType;
            if (!TryGetEntityType(navigationProperty.Type, out entityType))
                throw new UnresolvableObjectException(propertyName, string.Format("No association found for [{0}].", propertyName));
            return entityType.Name;
        }

        public override bool IsNavigationPropertyCollection(string collectionName, string propertyName)
        {
            var property = GetNavigationProperty(collectionName, propertyName);
            return property.Type.Definition.TypeKind == EdmTypeKind.Collection;
        }

        public override IEnumerable<string> GetDeclaredKeyPropertyNames(string collectionName)
        {
            var entityType = GetEntityType(collectionName);
            while (entityType.DeclaredKey == null && entityType.BaseEntityType() != null)
            {
                entityType = entityType.BaseEntityType();
            }

            if (entityType.DeclaredKey == null)
                return new string[] { };

            return entityType.DeclaredKey.Select(x => x.Name);
        }

        public override string GetFunctionFullName(string functionName)
        {
            var function = GetFunction(functionName);
            return function.Name;
        }

        public override EntityCollection GetFunctionReturnCollection(string functionName)
        {
            var function = GetFunction(functionName);

            if (function.ReturnType == null)
                return null;

            IEdmEntityType entityType;
            return !TryGetEntityType(function.ReturnType, out entityType) ? null : new EntityCollection(entityType.Name);
        }

        public override string GetFunctionVerb(string functionName)
        {
            var function = GetFunction(functionName);
            var annotation = _model.GetAnnotationValue(function, ODataNamespace.Metadata, "HttpMethod");
            return annotation is EdmStringConstant ? (annotation as EdmStringConstant).Value : RestVerbs.Get;
        }

        public override string GetActionFullName(string actionName)
        {
            return GetFunctionFullName(actionName);
        }

        public override EntityCollection GetActionReturnCollection(string actionName)
        {
            return GetFunctionReturnCollection(actionName);
        }

        private IEnumerable<IEdmEntitySet> GetEntitySets()
        {
            return _model.SchemaElements
                .Where(x => x.SchemaElementKind == EdmSchemaElementKind.EntityContainer)
                .SelectMany(x => (x as IEdmEntityContainer).EntitySets());
        }

        private IEdmEntitySet GetEntitySet(string entitySetName)
        {
            IEdmEntitySet entitySet;
            if (TryGetEntitySet(entitySetName, out entitySet))
                return entitySet;

            throw new UnresolvableObjectException(entitySetName, string.Format("Entity set [{0}] not found", entitySetName));
        }

        private bool TryGetEntitySet(string entitySetName, out IEdmEntitySet entitySet)
        {
            if (entitySetName.Contains("/"))
                entitySetName = entitySetName.Split('/').First();

            entitySet = _model.SchemaElements
                .Where(x => x.SchemaElementKind == EdmSchemaElementKind.EntityContainer)
                .SelectMany(x => (x as IEdmEntityContainer).EntitySets())
                .BestMatch(x => x.Name, entitySetName, _session.Settings.NameMatchResolver);

            return entitySet != null;
        }

        private IEnumerable<IEdmEntityType> GetEntityTypes()
        {
            return _model.SchemaElements
                .Where(x => x.SchemaElementKind == EdmSchemaElementKind.TypeDefinition && (x as IEdmType).TypeKind == EdmTypeKind.Entity)
                .Select(x => x as IEdmEntityType);
        }

        private IEdmEntityType GetEntityType(string collectionName)
        {
            IEdmEntityType entityType;
            if (TryGetEntityType(collectionName, out entityType))
                return entityType;

            throw new UnresolvableObjectException(collectionName, string.Format("Entity type [{0}] not found", collectionName));
        }

        private bool TryGetEntityType(string collectionName, out IEdmEntityType entityType)
        {
            entityType = null;
            if (collectionName.Contains("/"))
            {
                var segments = GetCollectionPathSegments(collectionName);

                if (SegmentsIncludeTypeSpecification(segments))
                {
                    var derivedTypeName = segments.Last();
                    var derivedType = GetEntityTypes().SingleOrDefault(x => x.FullName() == derivedTypeName);
                    if (derivedType != null)
                    {
                        entityType = derivedType;
                        return true;
                    }
                }
                else
                {
                    var collection = _session.Metadata.NavigateToCollection(collectionName);
                    entityType = GetEntityTypes().SingleOrDefault(x => x.Name == collection.Name);
                    if (entityType != null)
                    {
                        return true;
                    }
                }
            }
            else
            {
                var entitySet = GetEntitySets().BestMatch(x => x.Name, collectionName, _session.Settings.NameMatchResolver);
                if (entitySet != null)
                {
                    entityType = entitySet.ElementType;
                    return true;
                }

                var derivedType = GetEntityTypes().BestMatch(x => x.Name, collectionName, _session.Settings.NameMatchResolver);
                if (derivedType != null)
                {
                    var baseType = GetEntityTypes()
                        .SingleOrDefault(x => _model.FindDirectlyDerivedTypes(x).Contains(derivedType));
                    if (baseType != null && GetEntitySets().SingleOrDefault(x => x.ElementType == baseType) != null)
                    {
                        entityType = derivedType;
                        return true;
                    }
                    // Check if we can return it anyway
                    entityType = derivedType;
                    return true;
                }
            }

            return false;
        }

        private bool TryGetEntityType(IEdmTypeReference typeReference, out IEdmEntityType entityType)
        {
            entityType = typeReference.Definition.TypeKind == EdmTypeKind.Collection
                ? (typeReference.Definition as IEdmCollectionType).ElementType.Definition as IEdmEntityType
                : typeReference.Definition.TypeKind == EdmTypeKind.Entity
                ? typeReference.Definition as IEdmEntityType
                : null;
            return entityType != null;
        }

        private IEdmComplexType GetComplexType(string typeName)
        {
            IEdmComplexType complexType;
            if (TryGetComplexType(typeName, out complexType))
                return complexType;

            throw new UnresolvableObjectException(typeName, string.Format("ComplexType [{0}] not found", typeName));
        }

        private bool TryGetComplexType(string typeName, out IEdmComplexType complexType)
        {
            complexType = _model.SchemaElements
                .Where(x => x.SchemaElementKind == EdmSchemaElementKind.TypeDefinition && (x as IEdmType).TypeKind == EdmTypeKind.Complex)
                .Select(x => x as IEdmComplexType)
                .BestMatch(x => x.Name, typeName, _session.Settings.NameMatchResolver);

            return complexType != null;
        }

        private IEdmEnumType GetEnumType(string typeName)
        {
            IEdmEnumType enumType;
            if (TryGetEnumType(typeName, out enumType))
                return enumType;

            throw new UnresolvableObjectException(typeName, string.Format("Enum [{0}] not found", typeName));
        }

        private bool TryGetEnumType(string typeName, out IEdmEnumType enumType)
        {
            enumType = _model.SchemaElements
                .Where(x => x.SchemaElementKind == EdmSchemaElementKind.TypeDefinition && (x as IEdmType).TypeKind == EdmTypeKind.Enum)
                .Select(x => x as IEdmEnumType)
                .BestMatch(x => x.Name, typeName, _session.Settings.NameMatchResolver);

            return enumType != null;
        }

        private IEdmStructuralProperty GetStructuralProperty(string entitySetName, string propertyName)
        {
            var edmType = GetEntityType(entitySetName);
            return GetStructuralProperty(edmType, propertyName);
        }

        private IEdmStructuralProperty GetStructuralProperty(IEdmStructuredType edmType, string propertyName)
        {
            var property = edmType.StructuralProperties().BestMatch(
                x => x.Name, propertyName, _session.Settings.NameMatchResolver);

            if (property == null)
                throw new UnresolvableObjectException(propertyName, string.Format("Structural property [{0}] not found", propertyName));

            return property;
        }

        private IEdmNavigationProperty GetNavigationProperty(string entitySetName, string propertyName)
        {
            var property = GetEntityType(entitySetName).NavigationProperties().BestMatch(
                x => x.Name, propertyName, _session.Settings.NameMatchResolver);

            if (property == null)
                throw new UnresolvableObjectException(propertyName, string.Format("Navigation property [{0}] not found", propertyName));

            return property;
        }

        private IEdmFunctionImport GetFunction(string functionName)
        {
            var function = _model.SchemaElements
                .Where(x => x.SchemaElementKind == EdmSchemaElementKind.EntityContainer)
                .SelectMany(x => (x as IEdmEntityContainer).FunctionImports())
                .BestMatch(x => x.Name, functionName, _session.Settings.NameMatchResolver);

            if (function == null)
                throw new UnresolvableObjectException(functionName, string.Format("Function [{0}] not found", functionName));

            return function;
        }
    }
}