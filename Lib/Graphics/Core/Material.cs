using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.InteropServices;

using SlimDX;

namespace Lib
{
	/// <summary>
	/// マテリアル管理クラス
	/// </summary>
	public class Material
	{
		const int MAX_TEXTURE_NUM = 8;

		public enum MaterialType : int
		{
			COLOR_ONLY,
			TEXTURE_ONLY,
		};

		public struct Index4
		{
			public int i0, i1, i2, i3;

			public void Initialize()
			{
				i0 = i1 = i2 = i3 = -1;
			}

			public int this[int index] { 
				get {
					switch (index) {
						case 0: return i0;
						case 1: return i1;
						case 2: return i2;
						case 3: return i3;
					}
					return 0;
				}
				set {
					switch (index) {
						case 0: i0 = value; return;
						case 1: i1 = value; return;
						case 2: i2 = value; return;
						case 3: i3 = value; return;
					}
				}
			}

		}

		/// <summary>
		/// マテリアルデータ
		/// </summary>
		[StructLayout(LayoutKind.Explicit)]
		public struct ksMaterialStruct
		{
			[FieldOffset(0)]
			public int m_Type;

			[FieldOffset(4)]
			public Color4 color;

			[FieldOffset(4)]
			public Index4 m_texID;
		}


		// メンバ変数
		string name_;
		Object owner_;
		Shader shader_inst_ = null;
		IShaderView[] vsShaderViews_ = new IShaderView[MAX_TEXTURE_NUM];
		IShaderView[] psShaderViews_ = new IShaderView[MAX_TEXTURE_NUM];

		public Color4 diffuseColor_;
		public Vector4 generalParam_;
		public Vector4[] shCoef_;

		public IList<MaterialParam> userParams_;
		public int userParamSize_;
		public IList<TextureFileParam> textureFileParams_;
		
		public string Name { get { return name_; } set { name_ = value; } }
		public RenderState.BlendState BlendState { get; set; }
		public RenderState.DepthState DepthState { get; set; }
		public Vector4[] SHCoef { get { return shCoef_; } set { shCoef_ = value; } }
		public IShaderView[] VsShaderViews { get { return vsShaderViews_; } }
		public IShaderView[] PsShaderViews { get { return psShaderViews_; } }

		/// <summary>
		/// 
		/// </summary>
		public Material()
		{
			BlendState = RenderState.BlendState.None;
			DepthState = RenderState.DepthState.Normal;
		}


		/// <summary>
		/// マテリアルデータから初期化
		/// </summary>
		/// <param name="s"></param>
		public Material(Object owner, ref ksMaterialStruct s, List<Texture> textures)
		{
			owner_ = owner;
			BlendState = RenderState.BlendState.None;
			DepthState = RenderState.DepthState.Normal;

			if (s.m_Type == (int)MaterialType.COLOR_ONLY) {
				diffuseColor_ = s.color;
			} else {
				diffuseColor_ = new Color4(1, 1, 1, 1);
				for (int i = 0; i < 4; i++) {
					if (s.m_texID[i] < 0) break;
					psShaderViews_[i] = textures[s.m_texID[i]];
				}
			}
		}


		/// <summary>
		/// テクスチャパラム初期化
		/// </summary>
		/// <param name="s"></param>
		public void InitializeTextureParams(ref ksMaterialStruct s)
		{
			Model m = owner_ as Model;
			textureFileParams_ = new List<TextureFileParam>();
			for (int i = 0; i < 4; i++) {
				textureFileParams_.Add(new TextureFileParam());
				textureFileParams_[i].Index = i;
				if (s.m_Type == (int)Material.MaterialType.TEXTURE_ONLY && s.m_texID[i] >= 0) {
					textureFileParams_[i].Path = m.TextureDict[s.m_texID[i]];
				} else {
					textureFileParams_[i].Path = "";
				}
				textureFileParams_[i].Owner = this;
				textureFileParams_[i].PropertyChanged += MaterialTextureParam_PropertyChanged;
			}
		}

		/// <summary>
		/// 名前からシェーダセット
		/// </summary>
		/// <param name="name"></param>
		/// <param name="vertex_attr"></param>
		public void SetShader(string name, uint vertex_attr)
		{
			shader_inst_ = ShaderManager.FindShader(name, vertex_attr);
		}

		/// <summary>
		/// IDからシェーダセット
		/// </summary>
		/// <param name="id"></param>
		/// <param name="vertex_attr"></param>
		public void SetShader(uint id, uint vertex_attr)
		{
			shader_inst_ = ShaderManager.FindShader(id, vertex_attr);
		}

		/// <summary>
		/// シェーダを直でセット
		/// </summary>
		/// <param name="shader"></param>
		public void SetShader(Shader shader)
		{
			shader_inst_ = shader;
		}


		/// <summary>
		/// シェーダ切替
		/// </summary>
		/// <param name="name"></param>
		public void SetShader(string name)
		{
			shader_inst_ = ShaderManager.FindShader(name, shader_inst_.NeedVertexAttr);
		}

		/// <summary>
		/// シェーダ切替
		/// </summary>
		/// <param name="id"></param>
		public void ChangeShader(uint id)
		{
			shader_inst_ = ShaderManager.FindShader(id, shader_inst_.NeedVertexAttr);
		}


		/// <summary>
		/// テクスチャセット
		/// </summary>
		/// <param name="index"></param>
		/// <param name="texture"></param>
		public void SetShaderViewPS(int index, IShaderView texture)
		{
			psShaderViews_[index] = texture;
		}

		public void SetShaderViewVS(int index, IShaderView texture)
		{
			vsShaderViews_[index] = texture;
		}


		/// <summary>
		/// GPUにセットアップをかける
		/// </summary>
		public void Setup()
		{
			// シェーダ
			ShaderManager.BindShader(shader_inst_);

			// マテリアルパラメータ
			ShaderManager.SetMaterialParam(this);

			// テクスチャ
			for (int i = 0; i < psShaderViews_.Length; i++) {
				if (psShaderViews_[i] == null) break;
				GraphicsCore.SetShaderResourcePS(i, psShaderViews_[i]);
				Texture tex = psShaderViews_[i] as Texture;
				if (tex != null) {
					GraphicsCore.SetSamplerStatePS(i, tex.AddressingModeU, tex.AddressingModeV);
				}
			}
			for (int i = 0; i < vsShaderViews_.Length; i++) {
				if (vsShaderViews_[i] == null) break;
				GraphicsCore.SetShaderResourceVS(i, vsShaderViews_[i]);
				Texture tex = vsShaderViews_[i] as Texture;
				if (tex != null) {
					GraphicsCore.SetSamplerStateVS(i, tex.AddressingModeU, tex.AddressingModeV);
				}
			}

			// レンダーステート
			GraphicsCore.SetDepthState(DepthState);
			GraphicsCore.SetBlendState(BlendState);
		}


		/// <summary>
		/// テクスチャファイルパラムが更新されたときのイベントハンドラ
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		void MaterialTextureParam_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
		{
			TextureFileParam fp = sender as TextureFileParam;
			Model m = owner_ as Model;
			Material mat = fp.Owner;

			if (fp.Path == null || fp.Path == "") return;

			// モデルとの相対パスで設定
			Uri u1 = new Uri(System.IO.Path.GetDirectoryName(m.FilePath) + "/");
			Uri u2 = new Uri(fp.Path);
			string relativePath = u1.MakeRelativeUri(u2).ToString().Replace('/', '\\');

			int idx;
			// モデルがすでに持っているか検索
			if (m.TextureDict.ContainsValue(relativePath)) {
				idx = m.TextureDict.First(p => p.Value == relativePath).Key;
				mat.PsShaderViews[fp.Index] = m.Textures[idx];
			} else {
				// ない場合はテクスチャをセットアップ
				Texture texture = new Texture(fp.Path);
				idx = m.Textures.Count;
				m.Textures.Add(texture);
				m.TextureDict[idx] = relativePath;
				mat.PsShaderViews[fp.Index] = m.Textures[idx];
			}
			Material.Index4 idx4 = m.TextureIdDict[mat];
			idx4[fp.Index] = idx;
			m.TextureIdDict[mat] = idx4;
		}

	}
}
