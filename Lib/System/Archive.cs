using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Diagnostics;

namespace Lib
{
	/// <summary>
	/// アーカイブ（圧縮、結合ファイル)
	/// </summary>
	public class Archive
	{
		private byte[] data_;
		private int fileNum_;
		private BinaryReader reader_;

		public int FileNum { get { return fileNum_; } }

		/// <summary>
		/// 
		/// </summary>
		/// <param name="data"></param>
		public Archive(byte[] data)
		{
			data_ = data;

			MemoryStream s = new MemoryStream(data);
			reader_ = new BinaryReader(s);
			fileNum_ = reader_.ReadInt32();
			reader_.BaseStream.Position = 0;
		}


		/// <summary>
		/// インデックスのファイルを取得する
		/// </summary>
		/// <param name="index"></param>
		public byte[] GetFile(int index)
		{
			Debug.Assert(index < fileNum_);

			// ファイルまでのオフセットとサイズを求める
			reader_.BaseStream.Position = 4 + 4 * index;
			int offset = reader_.ReadInt32();
			int next_offset;
			if( index == fileNum_ - 1) {
				next_offset = (int)reader_.BaseStream.Length;
			} else {
				next_offset = reader_.ReadInt32();
			}
			int size = next_offset - offset;

			// オフセットをセットしてリード
			reader_.BaseStream.Position = offset;
			byte[] file_data = new byte[size];
			reader_.Read(file_data, 0, size);
			return file_data;
		}
	}
}
