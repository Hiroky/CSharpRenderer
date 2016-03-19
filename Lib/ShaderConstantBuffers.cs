using System;
using SlimDX;
using System.Runtime.InteropServices;
using System.Collections.Generic;
using System.Dynamic;


namespace Lib
{
	/// <summary>
	/// コンスタントバッファ定義用ダイナミックオブジェクト
	/// </summary>
	public class ConstantBufferInstance : DynamicObject
	{
		/// <summary>
		/// メンバ定義データ
		/// </summary>
		public struct VariableData
		{
			public Type type;
			public int offset;
			public int size;
		}

		byte[] buffer_;
		Dictionary<string, VariableData> variables_;

		public byte[] Buffer { get {return buffer_; } }
		public int BufferSize { get { return buffer_.Length; } }


		/// <summary>
		/// 初期化データを受け取ってコンスタントバッファ定義データを生成する
		/// </summary>
		/// <param name="var_table"></param>
		public ConstantBufferInstance(Dictionary<string, VariableData> var_table)
		{
			variables_ = var_table;

			int size = 0;
			foreach (var d in variables_) {
				size += d.Value.size;
			}
			buffer_ = new byte[size];
		}

		/// <summary>
		/// 名前を指定してbyte配列から特定の型で値を取得
		/// </summary>
		/// <param name="binder"></param>
		/// <param name="result"></param>
		/// <returns></returns>
		public override bool TryGetMember(GetMemberBinder binder, out object result)
		{
			if (variables_.ContainsKey(binder.Name)) {
				var d = variables_[binder.Name];
				unsafe {
					// 固定バイト配列領域に値をセットする
					fixed (byte* p = &buffer_[d.offset]) {
						result = Marshal.PtrToStructure((IntPtr)p, d.type);
					}
				}
			} else {
				result = null;
			}
			return true;
		}

		/// <summary>
		/// 名前を指定してbyte配列に値をセット
		/// </summary>
		/// <param name="binder"></param>
		/// <param name="value"></param>
		/// <returns></returns>
		public override bool TrySetMember(SetMemberBinder binder, object value)
		{
			if (variables_.ContainsKey(binder.Name)) {
				var d = variables_[binder.Name];
				if (d.type == value.GetType()) {
					unsafe {
						// 固定バイト配列領域に値をセットする
						fixed (byte* p = &buffer_[d.offset]) {
							Marshal.StructureToPtr(value, (IntPtr)p, false);
						}
					}
					return true;
				}
			}
			return false;
		}
	}


	//================================================================================
	//	以下のコンスタントバッファ定義用クラスはそのうち削除予定
	//  順次上のコンスタントバッファインスタンスに移行
	//================================================================================
	/// <summary>
	/// サイズを取得可能にするインターフェース
	/// </summary>
	class ISizeGettableObject
	{
		/// <summary>
		/// フィールドを列挙してクラスサイズを取得する
		/// </summary>
		/// <returns></returns>
		public int GetSize()
		{
			int size = 0;
			var members = GetType().GetFields();
			foreach (var m in members) {
				if (m.FieldType.Name == "Matrix[]") {
					var o = (Matrix[])m.GetValue(this);
					size += Marshal.SizeOf(o[0]) * o.Length;
				} else if (m.FieldType.Name == "Vector4[]") {
					var o = (Vector4[])m.GetValue(this);
					size += Marshal.SizeOf(o[0]) * o.Length;
				} else {
					size += Marshal.SizeOf(m.FieldType);
				}
			}
			return size;
		}

		/// <summary>
		/// フィールドを列挙して構造体のバイト配列を取得する
		/// </summary>
		/// <returns></returns>
		public byte[] GetBytes()
		{
			int size = GetSize();
			byte[] data = new byte[size];
			System.IO.MemoryStream s = new System.IO.MemoryStream(data);
			System.IO.BinaryWriter bw = new System.IO.BinaryWriter(s);
			var members = GetType().GetFields();
			foreach (var m in members) {
				switch (m.FieldType.Name) {
					case "float": {
							var o = (float)m.GetValue(this);
							bw.Write(o);
							break;
						}
					case "int": {
							var o = (int)m.GetValue(this);
							bw.Write(o);
							break;
						}
					case "uint": {
							var o = (uint)m.GetValue(this);
							bw.Write(o);
							break;
						}
					default: {
							var o = m.GetValue(this);
							byte[] byte_ary = new byte[Marshal.SizeOf(m.FieldType)];
							GCHandle h = GCHandle.Alloc(byte_ary, GCHandleType.Pinned);
							Marshal.StructureToPtr(o, h.AddrOfPinnedObject(), false);
							bw.Write(byte_ary);
							h.Free();
							break;
						}
				}
			}
			bw.Flush();
			return data;
		}
	}
}