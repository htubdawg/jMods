using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace HalfGripSlapPack
{
    // Imported game meshes discard their CPU copy. Read the existing GPU buffers into local arrays;
    // never change their readability, usage flags, vertices, materials, or renderer state.
    internal sealed class MeshSnapshot
    {
        internal Vector3[] Vertices;
        internal Vector2[] UV;
        internal int[][] Triangles;

        internal static MeshSnapshot Read(Mesh mesh)
        {
            var snapshot = new MeshSnapshot { Triangles = new int[mesh.subMeshCount][] };
            if (mesh.isReadable)
            {
                snapshot.Vertices = mesh.vertices;
                snapshot.UV = mesh.uv;
                for (int i = 0; i < mesh.subMeshCount; i++) snapshot.Triangles[i] = mesh.GetTriangles(i);
                return snapshot;
            }

            var streams = new Dictionary<int, byte[]>();
            snapshot.Vertices = new Vector3[mesh.vertexCount];
            for (int i = 0; i < mesh.vertexCount; i++)
                snapshot.Vertices[i] = new Vector3(Component(mesh, streams, VertexAttribute.Position, i, 0),
                    Component(mesh, streams, VertexAttribute.Position, i, 1), Component(mesh, streams, VertexAttribute.Position, i, 2));
            snapshot.UV = mesh.HasVertexAttribute(VertexAttribute.TexCoord0) ? new Vector2[mesh.vertexCount] : new Vector2[0];
            for (int i = 0; i < snapshot.UV.Length; i++)
                snapshot.UV[i] = new Vector2(Component(mesh, streams, VertexAttribute.TexCoord0, i, 0),
                    Component(mesh, streams, VertexAttribute.TexCoord0, i, 1));

            byte[] indices;
            using (GraphicsBuffer buffer = mesh.GetIndexBuffer()) indices = Copy(buffer);
            int indexSize = mesh.indexFormat == IndexFormat.UInt16 ? 2 : 4;
            for (int i = 0; i < mesh.subMeshCount; i++)
            {
                SubMeshDescriptor submesh = mesh.GetSubMesh(i);
                if (submesh.topology != MeshTopology.Triangles)
                    throw new NotSupportedException("Non-triangle topology in submesh " + i);
                int[] triangles = new int[submesh.indexCount];
                for (int j = 0; j < triangles.Length; j++)
                {
                    int offset = checked((submesh.indexStart + j) * indexSize);
                    int index = indexSize == 2 ? BitConverter.ToUInt16(indices, offset) : checked((int)BitConverter.ToUInt32(indices, offset));
                    triangles[j] = checked(index + submesh.baseVertex);
                    if (triangles[j] < 0 || triangles[j] >= snapshot.Vertices.Length)
                        throw new InvalidOperationException("GPU index exceeds the mesh vertex count.");
                }
                snapshot.Triangles[i] = triangles;
            }
            return snapshot;
        }

        private static float Component(Mesh mesh, Dictionary<int, byte[]> streams, VertexAttribute attribute, int vertex, int component)
        {
            if (!mesh.HasVertexAttribute(attribute) || component >= mesh.GetVertexAttributeDimension(attribute))
                throw new NotSupportedException("Missing vertex component for " + attribute);
            VertexAttributeFormat format = mesh.GetVertexAttributeFormat(attribute);
            int size = format == VertexAttributeFormat.Float32 ? 4 : format == VertexAttributeFormat.Float16 ? 2 : 0;
            if (size == 0) throw new NotSupportedException("Unsupported " + attribute + " format: " + format);
            int stream = mesh.GetVertexAttributeStream(attribute);
            if (!streams.TryGetValue(stream, out byte[] bytes))
            {
                using (GraphicsBuffer buffer = mesh.GetVertexBuffer(stream)) bytes = Copy(buffer);
                streams.Add(stream, bytes);
            }
            int offset = checked(vertex * mesh.GetVertexBufferStride(stream) + mesh.GetVertexAttributeOffset(attribute) + component * size);
            return size == 4 ? BitConverter.ToSingle(bytes, offset) : MeshBufferData.HalfToSingle(BitConverter.ToUInt16(bytes, offset));
        }

        private static byte[] Copy(GraphicsBuffer buffer)
        {
            if (buffer == null) throw new InvalidOperationException("Mesh GPU buffer is unavailable.");
            var bytes = new byte[checked(buffer.count * buffer.stride)];
            // A synchronous copy is intentional for this one-off diagnostic, not a per-frame path.
            buffer.GetData(bytes);
            return bytes;
        }
    }
}
