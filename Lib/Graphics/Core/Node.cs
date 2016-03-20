using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.InteropServices;

using SlimDX;

namespace Lib
{
	public class Node
	{
		enum NodeType : uint
		{
			Node,
			Joint,
		};

		/// <summary>
		/// 描画サブセット
		/// </summary>
		public struct DrawSubset
		{
			public int startIndex;
			public int endIndex;
			public int materialIndex;
			public int isSkin;
		}


		uint id_;
		NodeType type_;

		DrawSubset[] subsets_;

		Vector3 trans_;
		//Matrix bindPoseMatrix_;
		//Matrix invBindPoseMatrix_;

		public DrawSubset[] Subsets { get { return subsets_; } }

		Node parent_;
		Node child_;
		Node subling_;

		public Node Parent { get { return parent_; } set { parent_ = value; } }
		public Node Child { get { return child_; } set { child_ = value; } }
		public Node Subling { get { return subling_; } set { subling_ = value; } }

		public Node()
		{
		}

		public Node(int subsetNum)
		{
			trans_ = new Vector3(0, 0, 0);
			subsets_ = new DrawSubset[subsetNum];
		}

		/// <summary>
		/// データポインタから生成する
		/// </summary>
		/// <param name="ptr"></param>
		/// <returns></returns>
		public static Node CreateByBinary(ref IntPtr ptr)
		{
			Node o = new Node();

			o.id_ = (uint)Marshal.PtrToStructure(ptr, typeof(uint));
			ptr += 4;

			o.type_ = (NodeType)Marshal.PtrToStructure(ptr, typeof(uint));
			ptr += 4;

			if (o.type_ == NodeType.Node) {
				o.trans_ = (Vector3)Marshal.PtrToStructure(ptr, typeof(Vector3));
				ptr += 4 * 3;
			} else {
				// ボーンは現状未対応
				ptr += 4 * 12;
				ptr += 4 * 12;
			}

			int subsetNum = Marshal.ReadInt32(ptr);
			ptr += 4;


			if (subsetNum > 0) {
				o.subsets_ = new DrawSubset[subsetNum];
				int one_size = Marshal.SizeOf(typeof(DrawSubset));
				for (int i = 0; i < subsetNum; i++) {
					o.subsets_[i] = (DrawSubset)Marshal.PtrToStructure(ptr, typeof(DrawSubset));
					ptr += one_size;
				}

				//現状スキンデータ読み取りには非対応
			}

			return o;
		}
	}
}
