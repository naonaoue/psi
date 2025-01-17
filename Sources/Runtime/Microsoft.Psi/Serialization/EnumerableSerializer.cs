﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license.

namespace Microsoft.Psi.Serialization
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Microsoft.Psi.Common;

    /// <summary>
    /// Generates efficient code to serialize and deserialize an IEnumerable.
    /// </summary>
    /// <typeparam name="T">The type of objects this serializer knows how to handle.</typeparam>
    internal sealed class EnumerableSerializer<T> : ISerializer<IEnumerable<T>>
    {
        private const int Version = 2;

        private SerializationHandler<T> elementHandler;

        /// <inheritdoc />
        public bool? IsClearRequired => true;

        public TypeSchema Initialize(KnownSerializers serializers, TypeSchema targetSchema)
        {
            this.elementHandler = serializers.GetHandler<T>(); // register element type
            var type = typeof(T[]);
            var name = TypeSchema.GetContractName(type, serializers.RuntimeVersion);
            var elementsMember = new TypeMemberSchema("Elements", typeof(T).AssemblyQualifiedName, true);
            var schema = new TypeSchema(name, TypeSchema.GetId(name), type.AssemblyQualifiedName, TypeFlags.IsCollection, new TypeMemberSchema[] { elementsMember }, Version);
            return targetSchema ?? schema;
        }

        public void Serialize(BufferWriter writer, IEnumerable<T> instance, SerializationContext context)
        {
            writer.Write(instance.Count());
            foreach (T element in instance)
            {
                this.elementHandler.Serialize(writer, element, context);
            }
        }

        public void PrepareDeserializationTarget(BufferReader reader, ref IEnumerable<T> target, SerializationContext context)
        {
            var size = reader.ReadInt32();
            T[] buffTarget = target as T[];
            this.PrepareTarget(ref buffTarget, size, context);
            target = buffTarget;
        }

        public void Deserialize(BufferReader reader, ref IEnumerable<T> target, SerializationContext context)
        {
            var bufTarget = (T[])target; // by now target is guaranteed to be an array, because of PrepareDeserializationTarget
            for (int i = 0; i < bufTarget.Length; i++)
            {
                this.elementHandler.Deserialize(reader, ref bufTarget[i], context);
            }
        }

        public void PrepareCloningTarget(IEnumerable<T> instance, ref IEnumerable<T> target, SerializationContext context)
        {
            T[] buffTarget = target as T[];
            this.PrepareTarget(ref buffTarget, target.Count(), context);
            target = buffTarget;
        }

        public void Clone(IEnumerable<T> instance, ref IEnumerable<T> target, SerializationContext context)
        {
            var bufTarget = (T[])target; // by now target is guaranteed to be an array, because of PrepareCloningTarget
            int i = 0;
            foreach (var item in instance)
            {
                this.elementHandler.Clone(item, ref bufTarget[i++], context);
            }
        }

        public void Clear(ref IEnumerable<T> target, SerializationContext context)
        {
            T[] buffTarget = target.ToArray(); // this might allocate if target is not already an array
            for (int i = 0; i < buffTarget.Length; i++)
            {
                this.elementHandler.Clear(ref buffTarget[i], context);
            }

            target = buffTarget;
        }

        private void PrepareTarget(ref T[] target, int size, SerializationContext context)
        {
            if (target != null && target.Length > size && (!this.elementHandler.IsClearRequired.HasValue || this.elementHandler.IsClearRequired.Value))
            {
                // use a separate context to clear the unused objects, so that we don't corrupt the current context
                SerializationContext clearContext = new SerializationContext(context.Serializers);

                // only clear the extra items that we won't use during cloning or deserialization
                for (int i = size; i < target.Length; i++)
                {
                    this.elementHandler.Clear(ref target[i], clearContext);
                }
            }

            Array.Resize(ref target, size);
        }
    }
}
