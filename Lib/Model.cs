using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Runtime.InteropServices;
using System.Xml;
using System.Xml.Serialization;

using SlimDX;

namespace Lib
{
	/// <summary>
	/// モデル
	/// </summary>
	public class Model : IDisposable
	{
		// メンバ変数
		VertexBuffer vertexBuffer_;
		IndexBuffer indexBuffer_;
		Material[] materials_;
		List<Texture> textures_;

		int indexNum_;
		Node[] nodes_;
		Node controlNode_;
		Matrix worldMatrix_ = Matrix.Identity;

		public int MaterialNum { get { return materials_.Length; } }
		public Material[] Materials { get { return materials_; } }
		public List<Texture> Textures { get { return textures_; } }
		public Matrix WorldMatrix { get { return worldMatrix_; } set { worldMatrix_ = value; } }

		static bool autoCreateMaterialUserParam_ = false;
		public static bool AutoCreateMaterialUserParam { get { return autoCreateMaterialUserParam_; } set { autoCreateMaterialUserParam_ = value; } }

		// 調整用
		Dictionary<int, string> textureDict_;
		Dictionary<Material, Material.Index4> textureIdDict_;
		public string FilePath { get; set; }
		public Dictionary<int, string> TextureDict { get { return textureDict_; } }
		public Dictionary<Material, Material.Index4> TextureIdDict { get { return textureIdDict_; } }

		public Model()
		{
		}


		public Model(string filename, float scaling = 1.0f, bool calcTangent = false)
		{
			Initialize(filename, scaling, calcTangent);
		}


		/// <summary>
		/// 初期化
		/// </summary>
		public void Initialize(string fileName, float scaling = 1.0f, bool calcTangent = false)
		{
			string ext = System.IO.Path.GetExtension(fileName).ToLower();
			switch (ext) {
				case ".ksmdl":
					LoadFromksmdl(this, fileName);
					break;

				case ".obj":
					LoadFromObj(this, fileName, scaling, calcTangent);
					break;

				case ".mml":
					MMLParser p = MMLParser.Load(fileName);
					// 絶対パスにする
					string path = null;
					if( System.IO.Path.IsPathRooted(p.ModelPath) ){
						path = p.ModelPath;
					} else {
						path = System.IO.Path.GetDirectoryName(fileName) + "\\" + p.ModelPath;
					}
					string modelExt = System.IO.Path.GetExtension(p.ModelPath).ToLower();
					switch (modelExt) {
						case ".obj":
							LoadFromObj(this, path, scaling, calcTangent, p);
							break;
					}
					break;
			}
		}


		/// <summary>
		/// 廃棄
		/// </summary>
		public void Dispose()
		{
			if (vertexBuffer_ != null) vertexBuffer_.Dispose();
			if (indexBuffer_ != null) indexBuffer_.Dispose();
			if (textures_ != null) {
				foreach (var t in textures_) {
					t.Dispose();
				}
			}
		}


		/// <summary>
		/// 描画
		/// </summary>
		public void Draw()
		{
			vertexBuffer_.Bind();
			indexBuffer_.Bind();

			for (int i = 0; i < nodes_.Length; i++) {
				if (nodes_[i].Subsets == null) continue;
				foreach (var s in nodes_[i].Subsets) {
					materials_[s.materialIndex].Setup();
					ShaderManager.SetUniformParams(ref worldMatrix_);

					Renderer.D3dCurrentContext.DrawIndexed(s.endIndex - s.startIndex, s.startIndex, 0);
				}
			}
		}

		/// <summary>
		/// マテリアルを指定した描画
		/// </summary>
		public void Draw(string material)
		{
			vertexBuffer_.Bind();
			indexBuffer_.Bind();

			for (int i = 0; i < nodes_.Length; i++) {
				if (nodes_[i].Subsets == null) continue;
				foreach (var s in nodes_[i].Subsets) {
					if (materials_[s.materialIndex].Name == material) {
						materials_[s.materialIndex].Setup();
						ShaderManager.SetUniformParams(ref worldMatrix_);

						Renderer.D3dCurrentContext.DrawIndexed(s.endIndex - s.startIndex, s.startIndex, 0);
					}
				}
			}
		}



		public void SetShader(string shader_name)
		{
			foreach(var m in materials_){
				m.SetShader(shader_name);
			}
		}

		#region Loading Function

		/// <summary>
		/// データヘッダ
		/// </summary>
		struct ksModelHeader
		{
			public uint bufferBit;		// バッファ使用ビット
			public int nodeNum;			// ノード数
			public int vertexNum;		// 頂点数
			public int indexNum;		// インデックス数
		}


		/// <summary>
		/// バッファビット(そのうちShader::VertexAttrと統合する)
		/// </summary>
		enum BufferBit
		{
			Position = 0,
			Normal = 1 << 0,
			Texcoord0 = 1 << 1,
			Texcoord1 = 1 << 2,
			Texcoord2 = 1 << 3,
			SkinController = 1 << 4,
			Tangent = 1 << 5,
			Bitangent = 1 << 6,
		}


		/// <summary>
		/// バッファのストライドを求める
		/// </summary>
		static int GetVertexBufferStride(uint buffer_bit)
		{
			int stride = sizeof(float) * 3;

			if ((buffer_bit & (uint)BufferBit.Normal) != 0) {
				stride += sizeof(float) * 3;
			}
			if ((buffer_bit & (uint)BufferBit.Texcoord0) != 0) {
				stride += sizeof(float) * 2;
			}
			if ((buffer_bit & (uint)BufferBit.Tangent) != 0) {
				stride += sizeof(float) * 3;
			}
			if ((buffer_bit & (uint)BufferBit.Bitangent) != 0) {
				stride += sizeof(float) * 3;
			}
			if ((buffer_bit & (uint)BufferBit.SkinController) != 0) {
				stride += sizeof(float) * 4;
				stride += sizeof(uint);
			}

			return stride;
		}


		/// <summary>
		/// 階層情報読み取り
		/// </summary>
		static void ReadNodeHierarchy(ref IntPtr ptr, Node node, Node[] nodeList, bool setChild)
		{
			//ノードインデックス
			int nodeIndex = Marshal.ReadInt32(ptr);
			ptr += 4;

			Node current = nodeList[nodeIndex];

			if (!setChild) {
				node.Subling = current;
				current.Parent = node.Parent;
			} else {
				node.Child = current;
				current.Parent = node;
			}

			//子供数
			int childNum = Marshal.ReadInt32(ptr);
			ptr += 4;

			for (int i = 0; i < childNum; i++) {
				if (i == 0) {
					ReadNodeHierarchy(ref ptr, current, nodeList, true);
					current = current.Child;
				} else {
					ReadNodeHierarchy(ref ptr, current, nodeList, false);
					current = current.Subling;
				}
			}
		}

		/// <summary>
		/// ksmdlからロード
		/// </summary>
		static public Model LoadFromksmdl(string fileName)
		{
			var model = new Model(fileName);
			LoadFromksmdl(model, fileName);
			return model;
		}

		static void LoadFromksmdl(Model model, string fileName)
		{
			using (var stream = new System.IO.FileStream(fileName, System.IO.FileMode.Open, FileAccess.Read)) {
				byte[] data = new byte[stream.Length];
				stream.Read(data, 0, (int)stream.Length);
				MemoryStream s = new MemoryStream(data);

				// byte配列からポインタを取得
				var handle = GCHandle.Alloc(data, GCHandleType.Pinned);
				IntPtr ptr = handle.AddrOfPinnedObject();
				ksModelHeader header = (ksModelHeader)Marshal.PtrToStructure(ptr, typeof(ksModelHeader));
				int offset = Marshal.SizeOf(header);

				int stride = Model.GetVertexBufferStride(header.bufferBit);

				// 頂点バッファ
				IntPtr curPtr = ptr + offset;
				int vertex_size = stride * header.vertexNum;
				var vertices = new DataStream(curPtr, vertex_size, true, true);
				VertexBuffer.BufferDesc desc = new VertexBuffer.BufferDesc() { data = vertices, data_size = vertex_size, stride = stride };
				model.vertexBuffer_ = new VertexBuffer(desc);
				vertices.Close();

				// インデックスバッファ
				curPtr = curPtr + vertex_size;
				int index_size = sizeof(int) * header.indexNum;
				var indices = new DataStream(curPtr, index_size, true, true);
				model.indexBuffer_ = new IndexBuffer(indices, index_size);
				indices.Close();
				model.indexNum_ = header.indexNum;

				// マテリアル
				curPtr = curPtr + index_size;
				int material_num = Marshal.ReadInt32(curPtr);
				curPtr = curPtr + sizeof(int);
				Material.ksMaterialStruct[] material_struct_list = new Material.ksMaterialStruct[material_num];
				for (int i = 0; i < material_num; i++) {
					material_struct_list[i] = (Material.ksMaterialStruct)Marshal.PtrToStructure(curPtr, typeof(Material.ksMaterialStruct));
					{
						// ksmdlは現状1枚しかテクスチャに対応していない(設定しない分を-1で埋める)
						if (material_struct_list[i].m_Type == (int)(Material.MaterialType.TEXTURE_ONLY)) {
							for (int j = 1; j < 4; j++) {
								material_struct_list[i].m_texID[j] = -1;
							}
						}
					}
					curPtr += Marshal.SizeOf(typeof(Material.ksMaterialStruct));
				}

				// ノード
				model.nodes_ = new Node[header.nodeNum];
				for (int i = 0; i < header.nodeNum; ++i) {
					model.nodes_[i] = Node.CreateByBinary(ref curPtr);
				}

				// 階層情報
				model.controlNode_ = new Node();
				Node sublingNode = null;
				int hierarchyNum = Marshal.ReadInt32(curPtr);
				curPtr += 4;

				for (int i = 0; i < hierarchyNum; i++) {
					if (i == 0) {
						ReadNodeHierarchy(ref curPtr, model.controlNode_, model.nodes_, true);
						sublingNode = model.controlNode_.Child;
					} else {
						ReadNodeHierarchy(ref curPtr, sublingNode, model.nodes_, false);
						sublingNode = sublingNode.Subling;
					}
				}

				// テクスチャ
				int texture_num = Marshal.ReadInt32(curPtr);
				curPtr += 4;
				if (texture_num > 0) {
					model.textures_ = new List<Texture>(texture_num);
					for (int i = 0; i < texture_num; i++) {
						int size = Marshal.ReadInt32(curPtr);
						curPtr += 4;
						model.textures_[i] = new Texture(curPtr);
						curPtr += size;
					}
				}

				// マテリアル実体
				model.materials_ = new Material[material_num];
				for (int i = 0; i < material_num; i++) {
					model.materials_[i] = new Material(model, ref material_struct_list[i], model.textures_);
					//仮
					uint attr = (uint)(Shader.VertexAttr.POSITION | Shader.VertexAttr.NORMAL);
					attr |= (texture_num != 0) ? (uint)Shader.VertexAttr.TEXCOORD0 : 0;
					if ((header.bufferBit & (uint)BufferBit.SkinController) != 0) {
						attr |= (uint)Shader.VertexAttr.SKIN;
					}
					model.materials_[i].SetShader(ShaderManager.DefaultShader, attr);
				}

				// ハンドル解放
				handle.Free();
			}
		}


		/// <summary>
		/// OBJファイルからロード
		/// </summary>
		static public Model LoadFromObj(string fileName, float scaling = 1.0f, bool calcTangent = false)
		{
			var model = new Model();
			LoadFromObj(model, fileName, scaling, calcTangent);
			return model;
		}

		static void LoadFromObj(Model model, string fileName, float scaling = 1.0f, bool calcTangent = false, MMLParser mml = null)
		{
			string baseDir = Path.GetDirectoryName(fileName);
			string mtlPath = baseDir + "/" + Path.GetFileNameWithoutExtension(fileName) + ".mtl";
			var loader = new Ext.ObjFileLoader();
			loader.Load(fileName, calcTangent);
			loader.LoadMaterials(mtlPath);
			MeshLoad(model, loader, fileName, scaling, mml);
		}

		/// <summary>
		/// LoaderからMeshを生成する
		/// </summary>
		static void MeshLoad(Model model, Ext.MeshLoader loader, string fileName, float scaling, MMLParser mml)
		{
			model.FilePath = fileName;
			string baseDir = Path.GetDirectoryName(fileName);
			int numVertices = loader.m_Vertices.Count;
			int numIndices = loader.m_Indices.Count;

			// 頂点バッファ
			int elemNum;
			uint shaderAttr;
			if(loader.m_Vertices[0].GetType().Name == "MyVertex") {
				elemNum = 8;
				shaderAttr = (uint)(Shader.VertexAttr.POSITION | Shader.VertexAttr.NORMAL | Shader.VertexAttr.TEXCOORD0);
			} else {
				elemNum = 14;
				shaderAttr = (uint)(Shader.VertexAttr.POSITION | Shader.VertexAttr.NORMAL | Shader.VertexAttr.TEXCOORD0 | Shader.VertexAttr.TANGENT | Shader.VertexAttr.BITANGENT);
			}

			int stride = elemNum * sizeof(System.Single);
			int vertex_size = stride * numVertices;
			var vertices = new DataStream(vertex_size, true, true);
			foreach (var vertex in loader.m_Vertices) {
				vertices.Write(new Vector3(vertex.x * scaling, vertex.y * scaling, vertex.z * scaling));
				vertices.Write(new Vector3(vertex.nx, vertex.ny, vertex.nz));
				vertices.Write(new Vector2(vertex.u, vertex.v));

				var bv = vertex as Ext.MyVertexBump;
				if (bv != null) {
					vertices.Write(new Vector3(bv.tx, bv.ty, bv.tz));
					vertices.Write(new Vector3(bv.btx, bv.bty, bv.btz));
				}
			}
			vertices.Position = 0;
			VertexBuffer.BufferDesc desc = new VertexBuffer.BufferDesc() { data = vertices, data_size = vertex_size, stride = stride };
			model.vertexBuffer_ = new VertexBuffer(desc);
			vertices.Close();

			// インデックスバッファ
			int index_size = 4 * numIndices;
			var indices = new DataStream(index_size, true, true);
			foreach (var index in loader.m_Indices) {
				indices.Write(index);
			}
			indices.Position = 0;
			model.indexBuffer_ = new IndexBuffer(indices, index_size);
			indices.Close();
			model.indexNum_ = numIndices;

			// 以下マテリアル関係
			{
				// テクスチャ
				Dictionary<string, string> textureList = loader.m_textureList;
				if (mml != null) {
					textureList = new Dictionary<string, string>();
					foreach (var m in mml.Materials) {
						foreach (var t in m.Textures) {
							if (t.Path !=null && t.Path != "") {
								textureList[t.Path] = t.Path;
							}
						}
					}
				}
				int texNum = textureList.Count;
				if (texNum > 0) {
					model.textures_ = new List<Texture>(texNum);
				}
				var textureDict = new Dictionary<string, int>();
				model.textureDict_ = new Dictionary<int, string>();
				int currentIdx = 0;
				foreach (var v in textureList) {
					try {
						string path = v.Value.Replace("file:///", "");
						path = System.IO.Path.IsPathRooted(path) ? path : baseDir + "/" + path;
						model.textures_.Add(new Texture(path));
						textureDict[v.Key] = currentIdx;
						model.textureDict_[currentIdx] = v.Key;
						currentIdx++;
					} catch {
						Console.WriteLine("テクスチャの読み込みに失敗しました.");
					}
				}

				// マテリアル
				List<Ext.MaterialObject> materialList = loader.m_materialList;
				if (mml != null) {
					materialList = new List<Ext.MaterialObject>();
					// TODO:名前解決したほうがいい？
					foreach (var m in mml.Materials) {
						var mat = new Ext.MaterialObject();
						mat.ID = m.Name;
						mat.textureID = new List<string>();
						foreach (var s in m.Textures) {
							if (s.Path != null && s.Path != "") {
								mat.textureID.Add(s.Path);
							}
						}
						materialList.Add(mat);
					}
				}
				int material_num = materialList.Count;
				Material.ksMaterialStruct[] material_struct_list = new Material.ksMaterialStruct[material_num];
				for (int i = 0; i < material_num; i++) {
					if (materialList[i].textureID.Count > 0) {
						// テクスチャあり
						material_struct_list[i].m_Type = (int)Material.MaterialType.TEXTURE_ONLY;
						material_struct_list[i].m_texID.Initialize();

						for (int j = 0; j < materialList[i].textureID.Count; j++) {
							var t = materialList[i].textureID[j];
							if (textureDict.ContainsKey(t)) {
								material_struct_list[i].m_texID[j] = textureDict[t];
							}
						}
					} else {
						// テクスチャなし
						material_struct_list[i].m_Type = (int)Material.MaterialType.COLOR_ONLY;
						material_struct_list[i].color = new Color4(1.0f, 1.0f, 1.0f, 1.0f);
					}
				}

				// マテリアル実体
				model.materials_ = new Material[material_num];
				model.textureIdDict_ = new Dictionary<Material, Material.Index4>();
				for (int i = 0; i < material_num; i++) {
					model.materials_[i] = new Material(model, ref material_struct_list[i], model.textures_);
					model.materials_[i].Name = materialList[i].ID;		// 気持ち悪いので何とかしたいところ(ksmdlのサポートを切れば...)
					model.materials_[i].SetShader(ShaderManager.DefaultShader, shaderAttr);
					if (material_struct_list[i].m_Type == (int)Material.MaterialType.TEXTURE_ONLY) {
						model.textureIdDict_[model.materials_[i]] = material_struct_list[i].m_texID;
					}
					if (mml != null) {
						// パラメータ
						model.materials_[i].userParams_ = mml.Materials[i].Params;
						model.materials_[i].userParamSize_ = mml.ParamSize;
					}
				}

				// パラメータを持っていないデータを読んだ場合適当な値を生成しておく
				if (mml == null) {
					MaterialParamLoader mloader = new MaterialParamLoader(model.materials_);
					mloader.Load(null);
				}
				// マテリアルエディタ時は調整用パラメータを生成する
				if (AutoCreateMaterialUserParam) {
					for (int i = 0; i < material_num; i++) {
						model.materials_[i].InitializeTextureParams(ref material_struct_list[i]);
					}
				}

				// 1ノードで複数サブセットという形にしておく
				int subsetNum = loader.m_subsetList.Count;
				model.nodes_ = new Node[1];
				model.nodes_[0] = new Node(subsetNum);
				for (int i = 0; i < loader.m_subsetList.Count; i++) {
					var subset = loader.m_subsetList[i];
					int startIdx = subset.startIndex;
					int endIdx = subset.endIndex;
					model.nodes_[0].Subsets[i].startIndex = startIdx;
					model.nodes_[0].Subsets[i].endIndex = endIdx;
					model.nodes_[0].Subsets[i].materialIndex = materialList.FindIndex(0, c => c.ID == subset.material);
				}
			}
		}

		#endregion
	}


	[System.SerializableAttribute()]
	[System.Diagnostics.DebuggerStepThroughAttribute()]
	[System.ComponentModel.DesignerCategoryAttribute("code")]
	[System.Xml.Serialization.XmlTypeAttribute(AnonymousType = true)]
	[System.Xml.Serialization.XmlRootAttribute(ElementName = "ModelInfo", IsNullable = false)]
	public class MMLParser
	{
		[XmlElement(ElementName = "ModelPath")]
		public string ModelPath { get; set; }

		[System.Xml.Serialization.XmlIgnore]
		List<MMLMaterial> materials_;

		[XmlElement(ElementName = "Material")]
		public List<MMLMaterial> Materials { get { return materials_; } set { materials_ = value; } }

		[XmlIgnore]
		public int ParamSize { get; set; }

		/// <summary>
		/// 保存
		/// </summary>
		static public void Save(string fileName, Model model)
		{
			var serializer = new MMLParser();
			// モデルのパスを相対パスで設定
			Uri u1 = new Uri(System.IO.Path.GetDirectoryName(fileName) + "/");
			Uri u2 = new Uri(model.FilePath);
			string relativePath = u1.MakeRelativeUri(u2).ToString();
			serializer.ModelPath = relativePath;
			// マテリアル
			serializer.materials_ = new List<MMLMaterial>(model.MaterialNum);
			for (int i = 0; i < model.MaterialNum; i++) {
				var mat = new MMLMaterial();
				mat.Name = model.Materials[i].Name;
				mat.Params = new List<MaterialParam>(model.Materials[i].userParams_);
				mat.Textures = new List<TextureFileParam>(model.Materials[i].textureFileParams_);
				serializer.materials_.Add(mat);
			}

			XmlSerializer sw = new XmlSerializer(typeof(MMLParser));
			TextWriter tw = new StreamWriter(fileName);
			sw.Serialize(tw, serializer);
			tw.Close();
		}

		/// <summary>
		/// 読み込み
		/// </summary>
		static public MMLParser Load(string fileName)
		{
			XmlSerializer sr = new XmlSerializer(typeof(MMLParser));
			TextReader tr = new StreamReader(fileName);
			MMLParser doc = (MMLParser)(sr.Deserialize(tr));
			tr.Close();

			// パラメータのサイズを求める
			if (doc.Materials.Count > 0) {
				int size = 0;
				foreach (var p in doc.Materials[0].Params) {
					if (p.Value.GetType() == typeof(MaterialParamValue<float>)) {
						size += 4;
					} else if (p.Value.GetType() == typeof(MaterialParamValue<Color4>)) {
						size += 4 * 4;
					}
				}
				doc.ParamSize = size;
			}

			return doc;
		}
	}


	[System.SerializableAttribute()]
	[System.Xml.Serialization.XmlTypeAttribute(AnonymousType = true)]
	public class MMLMaterial
	{
		[XmlIgnore]
		List<MaterialParam> params_;

		[XmlIgnore]
		List<TextureFileParam> textures_;

		[XmlAttribute("Name")]
		public string Name { get; set; }

		[XmlElement(ElementName = "Parameter")]
		public List<MaterialParam> Params { get { return params_; } set { params_ = value; } }

		[XmlElement(ElementName = "Texture")]
		public List<TextureFileParam> Textures { get { return textures_; } set { textures_ = value; } }
	}
}
