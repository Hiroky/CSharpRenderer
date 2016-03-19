using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Runtime.InteropServices;

using SlimDX.D3DCompiler;
using SlimDX.Direct3D11;
using SlimDX.DXGI;
using D3DShaderReflection = SlimDX.D3DCompiler.ShaderReflection;
using D3D11Buffer = SlimDX.Direct3D11.Buffer;
using D3DComputeShader = SlimDX.Direct3D11.ComputeShader;


namespace Lib
{
	public class Shader : IDisposable
	{
		/// <summary>
		/// 頂点アトリビュート
		/// </summary>
		public enum VertexAttr : uint
		{
			POSITION = 0,		//必ずいるから0で
			COLOR = 1 << 0,		//頂点カラー
			NORMAL = 1 << 1,	//法線
			TANGENT = 1 << 2,	//接線
			BITANGENT = 1 << 3,	//従接線
			TEXCOORD0 = 1 << 4,	//UV0
			TEXCOORD1 = 1 << 5,	//UV1
			TEXCOORD2 = 1 << 6,	//UV2
			TEXCOORD3 = 1 << 7,	//UV3
			SKIN = 1 << 8,		//スキン(削除予定)
			SHADOW = 1 << 9,	//シャドウ(削除予定)
		};

		/// <summary>
		/// 頂点レイアウト
		/// </summary>
		enum LayoutID : int
		{
			Pos,
			PosTex,
			PosNrmTex,
			PosNrm,
			PosNrmTexSkin,
			PosNrmSkin,
			PosSkin,
			PosNrmTexTanBitan,
			PosColNrmTex,
			PosColNrm,
			PosColTex,

			Max,
		};

		//レイアウトアトリビュート定義
		static uint[] sLayoutAttrList = new uint[(int)LayoutID.Max] {
			(uint)VertexAttr.POSITION,
			(uint)(VertexAttr.POSITION | VertexAttr.TEXCOORD0),
			(uint)(VertexAttr.POSITION | VertexAttr.TEXCOORD0 | VertexAttr.NORMAL),
			(uint)(VertexAttr.POSITION | VertexAttr.NORMAL),
			(uint)(VertexAttr.POSITION | VertexAttr.TEXCOORD0 | VertexAttr.NORMAL | VertexAttr.SKIN),
			(uint)(VertexAttr.POSITION | VertexAttr.NORMAL | VertexAttr.SKIN),
			(uint)(VertexAttr.POSITION | VertexAttr.SKIN),
			(uint)(VertexAttr.POSITION | VertexAttr.TEXCOORD0 | VertexAttr.NORMAL | VertexAttr.TANGENT | VertexAttr.BITANGENT),
			(uint)(VertexAttr.POSITION | VertexAttr.COLOR | VertexAttr.TEXCOORD0 | VertexAttr.NORMAL),
			(uint)(VertexAttr.POSITION | VertexAttr.COLOR | VertexAttr.NORMAL),
			(uint)(VertexAttr.POSITION | VertexAttr.COLOR | VertexAttr.TEXCOORD0),
		};

		// レイアウトリスト
		static InputElement[][] sLayoutList = new InputElement[][] {
			new InputElement[] {
				new InputElement("POSITION", 0, Format.R32G32B32_Float, 0, 0, InputClassification.PerVertexData, 0 ),
			},
			new InputElement[] {
				new InputElement("POSITION", 0, Format.R32G32B32_Float, 0, 0, InputClassification.PerVertexData, 0),
				new InputElement("TEXCOORD", 0, Format.R32G32_Float, 12, 0, InputClassification.PerVertexData, 0 ),
			}, 
			new InputElement[] {
				new InputElement("POSITION", 0, Format.R32G32B32_Float, 0, 0, InputClassification.PerVertexData, 0 ),
				new InputElement("NORMAL", 0, Format.R32G32B32_Float, 12, 0, InputClassification.PerVertexData, 0 ),
				new InputElement("TEXCOORD", 0, Format.R32G32_Float, 24, 0, InputClassification.PerVertexData, 0 ),
			}, 
			new InputElement[] {
				new InputElement("POSITION", 0, Format.R32G32B32_Float, 0, 0, InputClassification.PerVertexData, 0 ),
				new InputElement("NORMAL", 0, Format.R32G32B32_Float, 12, 0, InputClassification.PerVertexData, 0 ),
			}, 
			new InputElement[] {
				new InputElement("POSITION", 0, Format.R32G32B32_Float, 0, 0, InputClassification.PerVertexData, 0 ),
				new InputElement("NORMAL", 0, Format.R32G32B32_Float, 12, 0, InputClassification.PerVertexData, 0 ),
				new InputElement("TEXCOORD", 0, Format.R32G32_Float, 24, 0,  InputClassification.PerVertexData, 0 ),
				new InputElement("WEIGHT", 0, Format.R32G32B32A32_Float, 32, 0, InputClassification.PerVertexData, 0 ),
				new InputElement("BONEINDEX", 0, Format.R32_Float, 48, 0, InputClassification.PerVertexData, 0 ),
			}, 
			new InputElement[] {
				new InputElement("POSITION", 0, Format.R32G32B32_Float, 0, 0, InputClassification.PerVertexData, 0 ),
				new InputElement("NORMAL", 0, Format.R32G32B32_Float, 12, 0, InputClassification.PerVertexData, 0 ),
				new InputElement("WEIGHT", 0, Format.R32G32B32A32_Float, 24, 0, InputClassification.PerVertexData, 0 ),
				new InputElement("BONEINDEX", 0, Format.R32_UInt, 40, 0, InputClassification.PerVertexData, 0 ),
			}, 
			new InputElement[] {
				new InputElement("POSITION", 0, Format.R32G32B32_Float, 0, 0, InputClassification.PerVertexData, 0 ),
				new InputElement("WEIGHT", 0, Format.R32G32B32A32_Float, 12, 0, InputClassification.PerVertexData, 0 ),
				new InputElement("BONEINDEX", 0, Format.R32_UInt, 28, 0, InputClassification.PerVertexData, 0 ),
			}, 
			new InputElement[] {
				new InputElement("POSITION", 0, Format.R32G32B32_Float, 0, 0, InputClassification.PerVertexData, 0 ),
				new InputElement("NORMAL", 0, Format.R32G32B32_Float, 12, 0, InputClassification.PerVertexData, 0 ),
				new InputElement("TEXCOORD", 0, Format.R32G32_Float, 24, 0, InputClassification.PerVertexData, 0 ),
				new InputElement("TANGENT", 0, Format.R32G32B32_Float, 32, 0, InputClassification.PerVertexData, 0 ),
				new InputElement("BITANGENT", 0, Format.R32G32B32_Float, 44, 0, InputClassification.PerVertexData, 0 ),
			}, 
			new InputElement[] {
				new InputElement("POSITION", 0, Format.R32G32B32_Float, 0, 0, InputClassification.PerVertexData, 0 ),
				new InputElement("COLOR", 0, Format.R32G32B32A32_Float, 12, 0, InputClassification.PerVertexData, 0 ),
				new InputElement("NORMAL", 0, Format.R32G32B32_Float, 28, 0, InputClassification.PerVertexData, 0 ),
				new InputElement("TEXCOORD", 0, Format.R32G32_Float, 40, 0, InputClassification.PerVertexData, 0 ),
			}, 
			new InputElement[] {
				new InputElement("POSITION", 0, Format.R32G32B32_Float, 0, 0, InputClassification.PerVertexData, 0 ),
				new InputElement("COLOR", 0, Format.R32G32B32A32_Float, 12, 0, InputClassification.PerVertexData, 0 ),
				new InputElement("NORMAL", 0, Format.R32G32B32_Float, 28, 0, InputClassification.PerVertexData, 0 ),
			}, 
			new InputElement[] {
				new InputElement("POSITION", 0, Format.R32G32B32_Float, 0, 0, InputClassification.PerVertexData, 0 ),
				new InputElement("COLOR", 0, Format.R32G32B32A32_Float, 12, 0, InputClassification.PerVertexData, 0 ),
				new InputElement("TEXCOORD", 0, Format.R32G32_Float, 28, 0, InputClassification.PerVertexData, 0 ),
			}, 
		};


		/// <summary>
		/// コンスタントバッファオブジェクト
		/// </summary>
		public class ConstantBufferObject
		{
			public string name_;
			public D3D11Buffer buffer_;
			public ConstantBufferInstance inst_;
			public Func<Shader, ConstantBufferInstance, bool> updateFunc_;
			public bool isSystem_;
		}


		/// <summary>
		/// コンスタントバッファバインドフラグ
		/// </summary>
		public enum SystemCBufferFlag : uint
		{
			TRANSFORM			= 1 << 0,
			USER				= 1 << 1,
		};

		/// <summary>
		/// コンスタントバッファ名のリスト
		/// </summary>
		readonly string[] SystemCBufferNameList = new string[2] {
			"CB_Transform",
			"CB_User",
		};


		/// <summary>
		/// シェーダバイナリヘッダ
		/// </summary>
		struct BinaryHeader
		{
			public uint id;
			public uint flag;
			public uint vs_size;
			public uint ps_size;
			public uint vs_offset;
			public uint ps_offset;
		};


		/// <summary>
		/// シェーダ初期化用
		/// </summary>
		public struct InitDesc
		{
			public string name;
			public uint id;
			public bool is_byte_code;
			public string file_name;
			public string vs_string;
			public string ps_string;
			public string vs_main;
			public string ps_main;
			public string profile;
			public ShaderMacro[] macro;
		};


		//
		// スタティック
		//
		const int MAX_BIND_CONSTANT_BUFFER_NUM = 5;
		static Dictionary<string, ConstantBufferObject> constantBufferDictionary_ = new Dictionary<string, ConstantBufferObject>();
		public static Dictionary<string, ConstantBufferObject> ConstantBufferDictionary { get { return constantBufferDictionary_; } }

		//
		// プライベートメンバ
		//
		string name_;
		uint shaderId_;
		uint vertexAttr_;
		VertexShader vertexShader_;
		PixelShader pixelShader_;
		InputLayout inputLayout_;

		List<ConstantBufferObject> constantBufferObjects_;
		D3D11Buffer[] nativeCBuffers_ = new D3D11Buffer[MAX_BIND_CONSTANT_BUFFER_NUM];
		uint cbufferBindFlag_;
		int cbufferNum_;
	
		
		//
		//プロパティ
		//
		public string Name { get { return name_; } }
		public uint ID { get { return shaderId_; } }
		public uint NeedVertexAttr { get { return vertexAttr_; } }
		public uint CBufferBindFlag { get { return cbufferBindFlag_; } }
		public bool Initilaized { get; set; }
		public List<ConstantBufferObject> ConstantBufferObjects { get { return constantBufferObjects_; } }


		/// <summary>
		/// 
		/// </summary>
		public Shader()
		{
			Initilaized = false;
		}

		/// <summary>
		/// ファイルから初期化
		/// </summary>
		public Shader(InitDesc desc)
		{
			Initilaized = false;
			Initialize(desc);
		}


		/// <summary>
		/// シェーダバイナリから生成
		/// </summary>
		/// <param name="data"></param>
		public Shader(byte[] data)
		{
			Initilaized = false;
			Initialize(data);
		}


		/// <summary>
		/// 初期化
		/// </summary>
		/// <param name="data"></param>
		public void Initialize(byte[] data)
		{
			var device = Renderer.D3dDevice;

			// byte配列からポインタを取得
			var handle = GCHandle.Alloc(data, GCHandleType.Pinned);
			IntPtr ptr = handle.AddrOfPinnedObject();
			BinaryHeader header = (BinaryHeader)Marshal.PtrToStructure(ptr, typeof(BinaryHeader));
			IntPtr vs_ptr = ptr + (int)header.vs_offset;
			IntPtr ps_ptr = ptr + (int)header.ps_offset;

			name_ = "0x" + header.id.ToString("x8");
			shaderId_ = header.id;

			// 頂点シェーダ
			using (ShaderBytecode bytecode = new ShaderBytecode(new SlimDX.DataStream(vs_ptr, header.vs_size, true, false))) {
				vertexShader_ = new VertexShader(device, bytecode);
				using (ShaderReflection reflect = new ShaderReflection(bytecode)) {
					// レイアウト生成
					vertexAttr_ = reflect.GetVertexLayoutAttribute();
					int layout_id = Shader.GetVertexLayoutID(vertexAttr_);
					inputLayout_ = new InputLayout(device, bytecode, sLayoutList[layout_id]);

					// コンスタントバッファ解決
					ResolveConstantBuffer(reflect);
				}
			}

			// ピクセルシェーダ
			using (ShaderBytecode bytecode = new ShaderBytecode(new SlimDX.DataStream(ps_ptr, header.ps_size, true, false))) {
				pixelShader_ = new PixelShader(device, bytecode);
				using (ShaderReflection reflect = new ShaderReflection(bytecode)) {
					// コンスタントバッファ解決
					ResolveConstantBuffer(reflect);
				}
			}

			// ハンドル解放
			handle.Free();

			Initilaized = true;
		}


		/// <summary>
		/// 初期化
		/// </summary>
		/// <param name="data"></param>
		public void Initialize(InitDesc desc)
		{
			var device = Renderer.D3dDevice;
			shaderId_ = desc.id;
			name_ = desc.name;

			if (desc.file_name != null) {
				// ファイルから生成
				// バイナリキャッシング
				string cacheDir = System.IO.Path.GetDirectoryName(desc.file_name) + "/cache";
				if(!System.IO.Directory.Exists(cacheDir)) {
					System.IO.Directory.CreateDirectory(cacheDir);
				}
				string cacheFile = cacheDir + "/" + desc.name + "_" + desc.vs_main + "_" + desc.ps_main;
				string vsCacheFile = cacheFile + ".vsc";
				string psCacheFile = cacheFile + ".psc";

				// 頂点シェーダキャッシュ読み取り
				ShaderBytecode vsBytecode = null;
				if (System.IO.File.Exists(vsCacheFile)) {
					DateTime cacheTime = System.IO.File.GetLastWriteTime(vsCacheFile);
					DateTime editTime = System.IO.File.GetLastWriteTime(desc.file_name);
					if (cacheTime > editTime) {
						System.IO.FileStream stream = new System.IO.FileStream(vsCacheFile, FileMode.Open);
						var bytes = new byte[stream.Length];
						stream.Read(bytes, 0, (int)stream.Length);
						vsBytecode = new ShaderBytecode(new SlimDX.DataStream(bytes, true, false));
					}
				}

				// キャッシュになければコンパイルして生成
				ShaderIncludeHandler inc = null;
				if (vsBytecode == null) {
					inc = new ShaderIncludeHandler(System.IO.Path.GetDirectoryName(desc.file_name));
					vsBytecode = ShaderBytecode.CompileFromFile(desc.file_name, desc.vs_main, "vs_" + desc.profile, ShaderFlags.None, EffectFlags.None, desc.macro, inc);
					// キャッシュファイル保存
					System.IO.FileStream stream = new System.IO.FileStream(vsCacheFile, FileMode.Create);
					vsBytecode.Data.CopyTo(stream);
					stream.Close();
				}

				using (vsBytecode) {
					vertexShader_ = new VertexShader(device, vsBytecode);
					using (ShaderReflection reflect = new ShaderReflection(vsBytecode)) {
						vertexAttr_ = reflect.GetVertexLayoutAttribute();
						int layout_id = Shader.GetVertexLayoutID(vertexAttr_);
						var signeture = ShaderSignature.GetInputSignature(vsBytecode);
						inputLayout_ = new InputLayout(device, signeture, sLayoutList[layout_id]);
						signeture.Dispose();

						// コンスタントバッファ解決
						ResolveConstantBuffer(reflect);
					}
				}

				// ピクセルシェーダキャッシュ読み取り
				if (desc.ps_main != null) {
					ShaderBytecode psBytecode = null;
					if (System.IO.File.Exists(psCacheFile)) {
						DateTime cacheTime = System.IO.File.GetLastWriteTime(psCacheFile);
						DateTime editTime = System.IO.File.GetLastWriteTime(desc.file_name);
						if (cacheTime > editTime) {
							System.IO.FileStream stream = new System.IO.FileStream(psCacheFile, FileMode.Open);
							var bytes = new byte[stream.Length];
							stream.Read(bytes, 0, (int)stream.Length);
							psBytecode = new ShaderBytecode(new SlimDX.DataStream(bytes, true, false));
						}
					}
					if (psBytecode == null) {
						if (inc == null) inc = new ShaderIncludeHandler(System.IO.Path.GetDirectoryName(desc.file_name));
						psBytecode = ShaderBytecode.CompileFromFile(desc.file_name, desc.ps_main, "ps_" + desc.profile, ShaderFlags.None, EffectFlags.None, desc.macro, inc);
						// キャッシュファイル保存
						System.IO.FileStream stream = new System.IO.FileStream(psCacheFile, FileMode.Create);
						psBytecode.Data.CopyTo(stream);
						stream.Close();
					}

					using (psBytecode) {
						pixelShader_ = new PixelShader(device, psBytecode);
						using (ShaderReflection reflect = new ShaderReflection(psBytecode)) {
							// コンスタントバッファ解決
							ResolveConstantBuffer(reflect);
						}
					}
				}
			} else {
				// stringから生成
				// 頂点シェーダ
				using (var bytecode = ShaderBytecode.Compile(desc.vs_string, desc.vs_main, "vs_" + desc.profile, ShaderFlags.None, EffectFlags.None)) {
					vertexShader_ = new VertexShader(device, bytecode);
					using (ShaderReflection reflect = new ShaderReflection(bytecode)) {
						vertexAttr_ = reflect.GetVertexLayoutAttribute();
						int layout_id = Shader.GetVertexLayoutID(vertexAttr_);
						var signeture = ShaderSignature.GetInputSignature(bytecode);
						inputLayout_ = new InputLayout(device, signeture, sLayoutList[layout_id]);
						signeture.Dispose();

						// コンスタントバッファ解決
						ResolveConstantBuffer(reflect);
					}
				}

				// ピクセルシェーダ
				if (desc.ps_main != null) {
					using (var bytecode = ShaderBytecode.Compile(desc.ps_string, desc.ps_main, "ps_" + desc.profile, ShaderFlags.None, EffectFlags.None)) {
						pixelShader_ = new PixelShader(device, bytecode);
						using (ShaderReflection reflect = new ShaderReflection(bytecode)) {
							// コンスタントバッファ解決
							ResolveConstantBuffer(reflect);
						}
					}
				}
			}
			Initilaized = true;
		}


		/// <summary>
		/// 終了処理
		/// </summary>
		public void Dispose()
		{
			if(inputLayout_ != null) inputLayout_.Dispose();
			if (vertexShader_ != null) vertexShader_.Dispose();
			if (pixelShader_ != null) pixelShader_.Dispose();
		}


		/// <summary>
		/// コンテキストにバインドする
		/// </summary>
		public void Bind()
		{
			var context = Renderer.D3dCurrentContext;
			context.InputAssembler.InputLayout = inputLayout_;
			context.VertexShader.Set(vertexShader_);
			context.PixelShader.Set(pixelShader_);

			// コンスタントバッファ
			context.VertexShader.SetConstantBuffers(nativeCBuffers_, 0, cbufferNum_);
			if (pixelShader_ != null) {
				context.PixelShader.SetConstantBuffers(nativeCBuffers_, 0, cbufferNum_);
			}
		}


		/// <summary>
		/// コンスタントバッファ解決
		/// </summary>
		/// <param name="reflection"></param>
		private
		void ResolveConstantBuffer(ShaderReflection reflection)
		{
			// すべてのコンスタントバッファを名前で辞書化する
			if (constantBufferObjects_ == null) {
				constantBufferObjects_ = new List<ConstantBufferObject>();
			}
			var list = reflection.GetConstantBufferNameList();
			foreach (var n in list) {
				int bind_index = 0;
				if (reflection.FindConstantBufferByName(n, out bind_index)) {
					if (!constantBufferDictionary_.ContainsKey(n)) {
						var obj = new ConstantBufferObject();
						obj.name_ = n;
						obj.isSystem_ = false;
						obj.inst_ = reflection.CreateConstantBufferInstance(n);

						BufferDescription buffer_desc = new BufferDescription();
						buffer_desc.Usage = ResourceUsage.Default;
						buffer_desc.CpuAccessFlags = CpuAccessFlags.None;
						buffer_desc.BindFlags = BindFlags.ConstantBuffer;
						buffer_desc.SizeInBytes = obj.inst_.BufferSize;
						obj.buffer_ = new D3D11Buffer(Renderer.D3dDevice, buffer_desc);
						constantBufferDictionary_[n] = obj;
					}

					// システムのCBufferか?
					if (SystemCBufferNameList.Contains(n)) {
						int index = 0;
						foreach (string s in SystemCBufferNameList) {
							if (s == n) break;
							index++;
						}
						cbufferBindFlag_ |= 1u << index;
						constantBufferDictionary_[n].isSystem_ = true;
					}

					constantBufferObjects_.Add(constantBufferDictionary_[n]);
					nativeCBuffers_[bind_index] = constantBufferDictionary_[n].buffer_;
					if (cbufferNum_ < (bind_index + 1)) {
						cbufferNum_ = bind_index + 1;
					}
				}
			}
		}


		/// <summary>
		/// 頂点レイアウトIDを取得する
		/// </summary>
		/// <param name="attr"></param>
		/// <returns></returns>
		private
		static int GetVertexLayoutID(uint attr)
		{
			//シャドウ等の直接頂点バッファレイアウトに関係ないアトリビュートはマスクする
			attr &= ~(uint)(VertexAttr.SHADOW);

			for(int i = 0; i < (int)LayoutID.Max; i++ ){
				if( sLayoutAttrList[i] == attr ) {
					return i;
				}
			}

			//見つからなかった
			Util.Assert(false);
			return (int)LayoutID.Max;
		}



		/// <summary>
		/// コンスタントバッファ廃棄
		/// </summary>
		public
		static void DisposeConstantBuffer()
		{
			foreach (var obj in constantBufferDictionary_) {
				if (obj.Value != null) {
					obj.Value.buffer_.Dispose();
				}
			}
		}


		/// <summary>
		/// CBufferの更新コールバックを設定
		/// </summary>
		/// <param name="name"></param>
		/// <param name="func"></param>
		public
		static void SetConstantBufferUpdateFunc(string name, Func<Shader, ConstantBufferInstance, bool> func)
		{
			if (ConstantBufferDictionary.ContainsKey(name)) {
				ConstantBufferDictionary[name].updateFunc_ = func;
			}
		}
	}



	/// <summary>
	/// シェーダリフレクション
	/// </summary>
	class ShaderReflection : IDisposable
	{
		static string[] sInputSemantics = new string[] {
				"POSITION",
				"NORMAL",
				"TEXCOORD",
				"TANGENT",
				"BITANGENT",
				"WEIGHT",
				"BONEINDEX",
				"COLOR",
			};
		static uint[] sInputAttribute = new uint[] {
				(uint)Shader.VertexAttr.POSITION,
				(uint)Shader.VertexAttr.NORMAL,
				(uint)Shader.VertexAttr.TEXCOORD0,
				(uint)Shader.VertexAttr.TANGENT,
				(uint)Shader.VertexAttr.BITANGENT,
				(uint)Shader.VertexAttr.SKIN,
				(uint)Shader.VertexAttr.SKIN,
				(uint)Shader.VertexAttr.COLOR,
			};

		D3DShaderReflection reflector_;


		public
		ShaderReflection(ShaderBytecode byteCode)
		{
			reflector_ = new D3DShaderReflection(byteCode);
		}


		public
		void Dispose()
		{
			reflector_.Dispose();
		}


		/// <summary>
		/// 頂点バッファレイアウトのアトリビュートを返す
		/// </summary>
		/// <returns></returns>
		public
		uint GetVertexLayoutAttribute()
		{
			uint attr = 0;
			ShaderDescription shader_desc = reflector_.Description;
			for (int i = 0; i < shader_desc.InputParameters; i++) {
				ShaderParameterDescription param_desc = reflector_.GetInputParameterDescription(i);
				for (int j = 0; j < sInputSemantics.Length; j++) {
					if (sInputSemantics[j] == param_desc.SemanticName) {
						attr |= sInputAttribute[j];
						break;
					}
				}
			}
			return attr;
		}


		/// <summary>
		/// コンスタントバッファの名前のリストを返す
		/// </summary>
		/// <returns></returns>
		public
		List<string> GetConstantBufferNameList()
		{
			var list = new List<string>();
			int cb_num = reflector_.Description.ConstantBuffers;
			for (int i = 0; i < cb_num; i++) {
				var tmp = reflector_.GetConstantBuffer(i);
				if (tmp.Description.Type == ConstantBufferType.ConstantBuffer) {
					list.Add(tmp.Description.Name);
				}
			}
			return list;
		}

		/// <summary>
		/// 名前からコンスタントバッファのバインド位置を取得する
		/// </summary>
		/// <param name="name"></param>
		/// <param name="bindIndex"></param>
		/// <returns></returns>
		public
		bool FindConstantBufferByName(string name, out int bindIndex)
		{
			// コンスタントバッファの実体をとってきて確実にある状況でGetResourceBindingDescriptionを呼ぶ
			int cb_num = reflector_.Description.ConstantBuffers;
			for (int i = 0; i < cb_num; i++) {
				var cb = reflector_.GetConstantBuffer(i);
				if (cb.Description.Name == name) {
					InputBindingDescription desc = reflector_.GetResourceBindingDescription(name);
					bindIndex = desc.BindPoint;
					return true;
				}
			}
			bindIndex = -1;
			return false;
		}

		/// <summary>
		/// コンスタントバッファインスタンスを返す
		/// </summary>
		/// <param name="name"></param>
		/// <returns></returns>
		public
		ConstantBufferInstance CreateConstantBufferInstance(string name)
		{
			// コンスタントバッファの実体をとってきて確実にある状況でGetResourceBindingDescriptionを呼ぶ
			int cb_num = reflector_.Description.ConstantBuffers;
			ConstantBuffer cb = null;
			for (int i = 0; i < cb_num; i++) {
				var tmp = reflector_.GetConstantBuffer(i);
				if (tmp.Description.Name == name) {
					cb = tmp;
				}
			}
			if (cb == null) return null;
			//Util.Assert(cb != null);

			// インスタンスを作って返す
			Dictionary<string, ConstantBufferInstance.VariableData> cbuffer_desc = new Dictionary<string, ConstantBufferInstance.VariableData>();
			for (int v = 0; v < cb.Description.Variables; v++) {
				var variable = cb.GetVariable(v);
				int offset = variable.Description.StartOffset;
				int size = variable.Description.Size;
				var var_type = variable.GetVariableType();
				System.Type type = null;
				switch (var_type.Description.Class) {
					case ShaderVariableClass.MatrixColumns:
						type = typeof(SlimDX.Matrix);
						break;
					case ShaderVariableClass.Vector:
						switch (var_type.Description.Columns) {
							case 2:
								type = typeof(SlimDX.Vector2);
								break;
							case 3:
								type = typeof(SlimDX.Vector3);
								break;
							case 4:
								type = typeof(SlimDX.Vector4);
								break;
						}
						break;
					case ShaderVariableClass.Scalar:
						switch(var_type.Description.Type) {
							case ShaderVariableType.Float:
								type = typeof(float);
								break;
							case ShaderVariableType.UInt:
								type = typeof(uint);
								break;
							case ShaderVariableType.Int:
								type = typeof(int);
								break;
						}
						break;
					default:
						Util.Assert(false);
						break;
				}
				//Console.WriteLine("{0}", type.Description.Class);
				if (var_type.Description.Elements == 0) {
					cbuffer_desc[variable.Description.Name] = new ConstantBufferInstance.VariableData() { type = type, offset = offset, size = size };
				} else {
					int cur_offset = offset;
					size = size / var_type.Description.Elements;
					for (int i = 0; i < var_type.Description.Elements; i++) {
						cbuffer_desc[variable.Description.Name + "_" + i] = new ConstantBufferInstance.VariableData() { type = type, offset = cur_offset, size = size };
						cur_offset += size;
					}
				}
			}
			return new ConstantBufferInstance(cbuffer_desc);
		}


		public
		bool FindUniformVariableByName(string name, out uint offset, out uint size)
		{
			offset = 0;
			size = 0;
			return true;
		}

		public
		bool FindTextureBindByName(string name, out uint bindIndex)
		{
			bindIndex = 0;
			return true;
		}
	}


	/// <summary>
	/// コンピュートシェーダ
	/// </summary>
	public class ComputeShader : IDisposable
	{
		/// <summary>
		/// シェーダ初期化用
		/// </summary>
		public struct InitDesc
		{
			public uint id;
			public bool is_byte_code;
			public string file_name;
			public string main;
			public ShaderMacro[] macro;
		};


		D3DComputeShader shader_;
		D3D11Buffer constantBuffer_;
		ConstantBufferInstance cbInst_;
		IShaderView[] resources_;
		IShaderView[] uavs_;

		public ConstantBufferInstance CBInstance { get { return cbInst_; } }

		/// <summary>
		/// 生成
		/// </summary>
		/// <param name="desc"></param>
		public ComputeShader(InitDesc desc)
		{
			var device = Renderer.D3dDevice;

			var inc = new ShaderIncludeHandler(System.IO.Path.GetDirectoryName(desc.file_name));
			using (var bytecode = ShaderBytecode.CompileFromFile(desc.file_name, desc.main, "cs_5_0", ShaderFlags.None, EffectFlags.None, desc.macro, inc)) {
				shader_ = new D3DComputeShader(device, bytecode);
				using (ShaderReflection reflect = new ShaderReflection(bytecode)) {
					// コンスタントバッファ解決
					cbInst_ = reflect.CreateConstantBufferInstance("CB");	// 現状名前固定
					if (cbInst_ != null) {
						BufferDescription buffer_desc = new BufferDescription();
						buffer_desc.Usage = ResourceUsage.Default;
						buffer_desc.CpuAccessFlags = CpuAccessFlags.None;
						buffer_desc.BindFlags = BindFlags.ConstantBuffer;
						buffer_desc.SizeInBytes = cbInst_.BufferSize;
						constantBuffer_ = new D3D11Buffer(Renderer.D3dDevice, buffer_desc);
					}
				}
			}
		}


		/// <summary>
		/// 廃棄
		/// </summary>
		public void Dispose()
		{
			shader_.Dispose();
			if (constantBuffer_ != null) {
				constantBuffer_.Dispose();
			}
		}

		/// <summary>
		/// コンスタントバッファ作成
		/// </summary>
		/// <param name="bufferSize"></param>
		public void CreateConstantBuffer(int bufferSize)
		{
			BufferDescription buffer_desc = new BufferDescription();
			buffer_desc.Usage = ResourceUsage.Default;
			buffer_desc.CpuAccessFlags = CpuAccessFlags.None;
			buffer_desc.BindFlags = BindFlags.ConstantBuffer;
			buffer_desc.SizeInBytes = bufferSize;
			constantBuffer_ = new D3D11Buffer(Renderer.D3dDevice, buffer_desc);
		}

		/// <summary>
		/// コンスタントバッファコンスタントバッファ更新
		/// </summary>
		/// <param name="inst"></param>
		public void UpdateConstantBuffer()
		{
			byte[] data = cbInst_.Buffer;
			SlimDX.DataStream s = new SlimDX.DataStream(data, true, true);
			SlimDX.DataBox box = new SlimDX.DataBox(0, 0, s);
			Renderer.D3dCurrentContext.UpdateSubresource(box, constantBuffer_, 0);
			s.Close();
		}

		public void SetResources(IShaderView[] resources)
		{
			resources_ = resources;
		}

		public void SetUAVs(IShaderView[] uavs)
		{
			uavs_ = uavs;
		}

		/// <summary>
		///  バインド
		/// </summary>
		public void Bind()
		{
			var context = Renderer.D3dCurrentContext;
			context.ComputeShader.Set(shader_);
			context.ComputeShader.SetConstantBuffer(constantBuffer_, 0);
			if( resources_ != null ) {
				ShaderResourceView[] srvs = new ShaderResourceView[resources_.Length];
				for (int i = 0; i < resources_.Length; i++) {
					srvs[i] = resources_[i].ShaderResourceView;
				}
				context.ComputeShader.SetShaderResources(srvs, 0, srvs.Length);
			}
			if (uavs_ != null) {
				UnorderedAccessView[] uavs = new UnorderedAccessView[uavs_.Length];
				for (int i = 0; i < uavs_.Length; i++) {
					uavs[i] = uavs_[i].UnorderedAccessView;
				}
				context.ComputeShader.SetUnorderedAccessViews(uavs, 0, uavs.Length);
			}
		}


		/// <summary>
		/// 廃棄
		/// </summary>
		public void UnBind()
		{
			var context = Renderer.D3dCurrentContext;
			context.ComputeShader.Set(null);
			context.ComputeShader.SetConstantBuffer(null, 0);
			if (resources_ != null) {
				context.ComputeShader.SetShaderResources(new ShaderResourceView[resources_.Length], 0, resources_.Length);
			}
			if (uavs_ != null) {
				context.ComputeShader.SetUnorderedAccessViews(new UnorderedAccessView[uavs_.Length], 0, uavs_.Length);
			}
		}


		/// <summary>
		/// 実行
		/// </summary>
		public void Dispatch(int x, int y, int z)
		{
			Renderer.D3dCurrentContext.Dispatch(x, y, z);
		}

		/// <summary>
		/// ディスパッチ数を返す
		/// </summary>
		/// <param name="workItems"></param>
		/// <param name="groupItems"></param>
		/// <returns></returns>
		public static int CalcThreadGroups(int workItems, int groupItems)
		{
			int num = (workItems + groupItems - 1) / groupItems;
			return num;
		}
	}


	/// <summary>
	/// シェーダコンパイル時のインクルード解決のハンドラ
	/// </summary>
	public class ShaderIncludeHandler : Include
	{
		string rootDir_;

		public ShaderIncludeHandler(string root)
		{
			rootDir_ = root;
		}

		public void Close(Stream stream)
		{
			stream.Close();
			stream.Dispose();
		}

		public void Open(IncludeType type, string fileName, Stream parentStream, out Stream stream)
		{
			stream = new System.IO.FileStream(rootDir_ + "/" + fileName, FileMode.Open);
		}
	}
}
