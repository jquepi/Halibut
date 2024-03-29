﻿using System;
using System.IO.Compression;
#if !NETFRAMEWORK
using System.Reflection;
#endif

namespace Halibut.Transport.Protocol
{

    #if NETFRAMEWORK
    /// <summary>
    /// Not supported on NETFRAMEWORK. Reflects <see cref="DeflateStream"/> to determine the number of bytes available in its input buffer.
    /// </summary>
    class DeflateStreamInputBufferReflector
    {
        public bool TryGetAvailableInputBufferSize(DeflateStream stream, out uint unusedSizeBytes)
        {
            throw new PlatformNotSupportedException();
        }
    }
    #else

    /// <summary>
    /// Reflects <see cref="DeflateStream"/> to determine the number of bytes available in an instance's input buffer.
    /// </summary>
    /// <remarks>
    /// When <see cref="DeflateStream"/> fills its buffer, it can consume uncompressed bytes from the stream that appear after
    /// the compressed bytes. Once deflation is complete, any bytes left in the internal zlib stream buffer are these uncompressed bytes.
    ///
    /// Knowing the number of over-consumed bytes is important, because Halibut protocol control messages may have been inadvertently consumed.
    /// See: https://github.com/OctopusDeploy/Halibut/pull/154
    /// </remarks>
    class DeflateStreamInputBufferReflector
    {
        readonly bool canReflect;
        FieldInfo inflaterField;
        FieldInfo zlibStreamField;
        PropertyInfo availInProperty;

        public DeflateStreamInputBufferReflector()
        {
            CacheTypeInfo();
            canReflect = inflaterField != null && zlibStreamField != null && availInProperty != null;
        }

        public bool TryGetAvailableInputBufferSize(DeflateStream stream, out uint inputBufferAvailSize)
        {
            if (!canReflect)
            {
                inputBufferAvailSize = 0;
                return false;
            }

            var inflater = inflaterField.GetValue(stream);
            if (inflater == null)
            {
                inputBufferAvailSize = 0;
                return false;
            }

            var zlibStream = zlibStreamField.GetValue(inflater);
            if (zlibStream is null)
            {
                inputBufferAvailSize = 0;
                return false;
            }

            var size = (uint?)availInProperty.GetValue(zlibStream);
            if (size is null)
            {
                inputBufferAvailSize = 0;
                return false;
            }

            inputBufferAvailSize = size.Value;
            return true;
        }

        void CacheTypeInfo()
        {
            inflaterField = typeof(DeflateStream).GetField("_inflater", BindingFlags.NonPublic | BindingFlags.Instance);
            zlibStreamField = inflaterField?.FieldType.GetField("_zlibStream", BindingFlags.NonPublic | BindingFlags.Instance);
            availInProperty = zlibStreamField?.FieldType.GetProperty("AvailIn");
        }
    }
    #endif
}
