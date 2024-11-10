// <auto-generated>
//  automatically generated by the FlatBuffers compiler, do not modify
// </auto-generated>

using global::System;
using global::System.Collections.Generic;
using global::Google.FlatBuffers;

public struct Chunk : IFlatbufferObject
{
  private Table __p;
  public ByteBuffer ByteBuffer { get { return __p.bb; } }
  public static void ValidateVersion() { FlatBufferConstants.FLATBUFFERS_24_3_25(); }
  public static Chunk GetRootAsChunk(ByteBuffer _bb) { return GetRootAsChunk(_bb, new Chunk()); }
  public static Chunk GetRootAsChunk(ByteBuffer _bb, Chunk obj) { return (obj.__assign(_bb.GetInt(_bb.Position) + _bb.Position, _bb)); }
  public void __init(int _i, ByteBuffer _bb) { __p = new Table(_i, _bb); }
  public Chunk __assign(int _i, ByteBuffer _bb) { __init(_i, _bb); return this; }

  public sbyte Hash(int j) { int o = __p.__offset(4); return o != 0 ? __p.bb.GetSbyte(__p.__vector(o) + j * 1) : (sbyte)0; }
  public int HashLength { get { int o = __p.__offset(4); return o != 0 ? __p.__vector_len(o) : 0; } }
#if ENABLE_SPAN_T
  public Span<sbyte> GetHashBytes() { return __p.__vector_as_span<sbyte>(4, 1); }
#else
  public ArraySegment<byte>? GetHashBytes() { return __p.__vector_as_arraysegment(4); }
#endif
  public sbyte[] GetHashArray() { return __p.__vector_as_array<sbyte>(4); }
  public long Size { get { int o = __p.__offset(6); return o != 0 ? __p.bb.GetLong(o + __p.bb_pos) : (long)0; } }
  public long Offset { get { int o = __p.__offset(8); return o != 0 ? __p.bb.GetLong(o + __p.bb_pos) : (long)0; } }

  public static Offset<Chunk> CreateChunk(FlatBufferBuilder builder,
      VectorOffset hashOffset = default(VectorOffset),
      long size = 0,
      long offset = 0) {
    builder.StartTable(3);
    Chunk.AddOffset(builder, offset);
    Chunk.AddSize(builder, size);
    Chunk.AddHash(builder, hashOffset);
    return Chunk.EndChunk(builder);
  }

  public static void StartChunk(FlatBufferBuilder builder) { builder.StartTable(3); }
  public static void AddHash(FlatBufferBuilder builder, VectorOffset hashOffset) { builder.AddOffset(0, hashOffset.Value, 0); }
  public static VectorOffset CreateHashVector(FlatBufferBuilder builder, sbyte[] data) { builder.StartVector(1, data.Length, 1); for (int i = data.Length - 1; i >= 0; i--) builder.AddSbyte(data[i]); return builder.EndVector(); }
  public static VectorOffset CreateHashVectorBlock(FlatBufferBuilder builder, sbyte[] data) { builder.StartVector(1, data.Length, 1); builder.Add(data); return builder.EndVector(); }
  public static VectorOffset CreateHashVectorBlock(FlatBufferBuilder builder, ArraySegment<sbyte> data) { builder.StartVector(1, data.Count, 1); builder.Add(data); return builder.EndVector(); }
  public static VectorOffset CreateHashVectorBlock(FlatBufferBuilder builder, IntPtr dataPtr, int sizeInBytes) { builder.StartVector(1, sizeInBytes, 1); builder.Add<sbyte>(dataPtr, sizeInBytes); return builder.EndVector(); }
  public static void StartHashVector(FlatBufferBuilder builder, int numElems) { builder.StartVector(1, numElems, 1); }
  public static void AddSize(FlatBufferBuilder builder, long size) { builder.AddLong(1, size, 0); }
  public static void AddOffset(FlatBufferBuilder builder, long offset) { builder.AddLong(2, offset, 0); }
  public static Offset<Chunk> EndChunk(FlatBufferBuilder builder) {
    int o = builder.EndTable();
    return new Offset<Chunk>(o);
  }
  public ChunkT UnPack() {
    var _o = new ChunkT();
    this.UnPackTo(_o);
    return _o;
  }
  public void UnPackTo(ChunkT _o) {
    _o.Hash = new List<sbyte>();
    for (var _j = 0; _j < this.HashLength; ++_j) {_o.Hash.Add(this.Hash(_j));}
    _o.Size = this.Size;
    _o.Offset = this.Offset;
  }
  public static Offset<Chunk> Pack(FlatBufferBuilder builder, ChunkT _o) {
    if (_o == null) return default(Offset<Chunk>);
    var _hash = default(VectorOffset);
    if (_o.Hash != null) {
      var __hash = _o.Hash.ToArray();
      _hash = CreateHashVector(builder, __hash);
    }
    return CreateChunk(
      builder,
      _hash,
      _o.Size,
      _o.Offset);
  }
}

public class ChunkT
{
  [Newtonsoft.Json.JsonProperty("hash")]
  public List<sbyte> Hash { get; set; }
  [Newtonsoft.Json.JsonProperty("size")]
  public long Size { get; set; }
  [Newtonsoft.Json.JsonProperty("offset")]
  public long Offset { get; set; }

  public ChunkT() {
    this.Hash = null;
    this.Size = 0;
    this.Offset = 0;
  }
}
