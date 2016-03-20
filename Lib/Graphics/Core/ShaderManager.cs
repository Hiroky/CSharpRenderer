using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.InteropServices;
using System.IO;

using SlimDX;
using SlimDX.D3DCompiler;
using SlimDX.Direct3D11;
using SlimDX.DXGI;
using SlimDX.Windows;



namespace Lib
{
	/// <summary>
	/// シェーダ管理クラス
	/// </summary>
	public static class ShaderManager
	{
		static Archive shaderArc_;
		static List<Shader> shaderArray_;
		static string fileName_;

		static Shader activeShader_;

		static Matrix worldMatrixCache_;
		static Matrix viewMatrixCache_;

		static public string DefaultShader { get; set; }

		static public Func<Shader, Shader> UserShaderBindHandler { get; set; }

		/// <summary>
		/// 初期化
		/// </summary>
		/// <param name="arc_file_name">シェーダバイナリアーカイブファイル名</param>
		static public void Initialize(string fileName)
		{
			fileName_ = fileName;
			shaderArray_ = new List<Shader>();
			DefaultShader = "Unlit";
			UserShaderBindHandler = null;

			Load();
		}

		/// <summary>
		/// ファイルタイプを識別してロード
		/// </summary>
		static void Load()
		{
			string ext = System.IO.Path.GetExtension(fileName_);
			switch (ext) {
				case ".bin":
					LoadFromBinFile(fileName_);
					break;

				case ".lst":
				case ".txt":
				case ".ini":
					LoadFromListFile(fileName_);
					break;
			}
		}

		/// <summary>
		/// バイナリから初期化
		/// </summary>
		/// <param name="binFile"></param>
		static void LoadFromBinFile(string binFile)
		{
			// シェーダバイナリからシェーダインスタンスを初期化
			using (var stream = new System.IO.FileStream(binFile, System.IO.FileMode.Open)) {
				byte[] ary = new byte[stream.Length];
				stream.Read(ary, 0, (int)stream.Length);
				shaderArc_ = new Archive(ary);
			}

			for (int i = 0; i < shaderArc_.FileNum; i++) {
				if (i < shaderArray_.Count) {
					// リロード
					shaderArray_[i].Dispose();
					shaderArray_[i].Initialize(shaderArc_.GetFile(i));
				} else {
					shaderArray_.Add(new Shader(shaderArc_.GetFile(i)));
				}
			}
		}

		/// <summary>
		/// リストファイルからロードする(ランタイムコンパイル)
		/// </summary>
		/// <param name="listFile"></param>
		static void LoadFromListFile(string listFile)
		{
			using (StreamReader sr = new StreamReader(listFile)) {
				string rootDir = System.IO.Path.GetDirectoryName(listFile);
				bool end = false;
				int count = 0;
				while (!end) {
					if (sr.EndOfStream) {
						end = true;
						continue;
					}
					string line = sr.ReadLine().Trim();
					if (line.Length == 0 || line[0] == '#') {
						continue;
					}

					// コマンドパース
					string[] args = line.Split(' ');
					string name = args[0];
					string file = args[1];
					string vsEntry = null;
					string psEntry = null;
					List<string> defineList = new List<string>();
					for (int i = 2; i < args.Length; i++) {
						switch (args[i]) {
							case "-vs":
								vsEntry = args[++i];
								break;
							case "-ps":
								psEntry = args[++i];
								break;
							case "-D":
								defineList.Add(args[++i]);
								break;
						}
					}

					ShaderMacro[] macro = null;
					if(defineList.Count > 0){
						macro = new ShaderMacro[defineList.Count];
						int i = 0;
						foreach (var d in defineList) {
							macro[i] = new ShaderMacro(d);
							i++;
						}
					}

					// コンパイル
					Shader.InitDesc shaderDesc = new Shader.InitDesc {
						name = name,
						file_name = rootDir + "/" + file,
						id = Util.CalcCrc32(name),
						profile = "5_0",
						vs_main = vsEntry,
						ps_main = psEntry,
						macro = macro,
					};
					if (count < shaderArray_.Count) {
						// リロード
						shaderArray_[count].Dispose();
						shaderArray_[count].Initialize(shaderDesc);
					} else {
						shaderArray_.Add(new Shader(shaderDesc));
					}
					count++;
				}
			}
		}

		/// <summary>
		/// 終了処理
		/// </summary>
		static public void Dispose()
		{
			foreach (var s in shaderArray_) {
				s.Dispose();
			}
			Shader.DisposeConstantBuffer();
		}


		static public void Reload(string arc_file_name)
		{
#if false
			// シェーダバイナリからシェーダインスタンスを初期化
			using (var stream = new System.IO.FileStream(arc_file_name, System.IO.FileMode.Open)) {
				byte[] ary = new byte[stream.Length];
				stream.Read(ary, 0, (int)stream.Length);
				shaderArc_ = new Archive(ary);
			}

			foreach (var s in shaderArray_) {
				s.Dispose();
			}
			for (int i = 0; i < shaderArc_.FileNum; i++) {
				shaderArray_[i].Initialize(shaderArc_.GetFile(i));
			}
#else
			Load();
#endif
		}

		/// <summary>
		/// シェーダ検索
		/// </summary>
		/// <param name="name"></param>
		/// <param name="vertex_attr"></param>
		/// <returns></returns>
		static public Shader FindShader(string name, uint vertex_attr)
		{
			uint id = Util.CalcCrc32(name);
			return FindShader(id, vertex_attr);
		}

		/// <summary>
		/// シェーダ検索
		/// </summary>
		/// <param name="id"></param>
		/// <param name="vertex_attr"></param>
		/// <returns></returns>
		static public Shader FindShader(uint id, uint vertex_attr)
		{
			//検索
			foreach (Shader s in shaderArray_) {
				if (s.ID == id) {
					if (s.NeedVertexAttr == vertex_attr) {
						return s;
					}
				}
			}
			Util.Assert(false);
			return null;
		}



		/// <summary>
		/// シェーダのセットアップ
		/// </summary>
		/// <param name="shader"></param>
		public
		static void BindShader(Shader shader)
		{
			// シェーダオーバーライド
			if (UserShaderBindHandler != null) {
				shader = UserShaderBindHandler(shader);
			}

			//使用シェーダに変更がある場合のみバインド
			if (activeShader_ != shader) {
				activeShader_ = shader;
				activeShader_.Bind();
			}
		}


		/// <summary>
		/// ユニフォームバッファの更新
		/// </summary>
		public
		static void SetUniformParams(ref Matrix worldMatrix)
		{
			uint flag = activeShader_.CBufferBindFlag;
			if ((flag & (uint)Shader.SystemCBufferFlag.TRANSFORM) != 0) {
				var cbTrans = Shader.ConstantBufferDictionary["CB_Transform"];
				Camera camera = Renderer.CurrentDrawCamera;
				dynamic cb = cbTrans.inst_;

				// 行列キャッシュ
				if (worldMatrixCache_ != worldMatrix || viewMatrixCache_ != camera.ViewMatrix) {
					// やっぱりコピーの無駄が多すぎる…ちょっと自前の実装考えたほうがいいかも…
					Matrix mv = Matrix.Multiply(worldMatrix, camera.ViewMatrix);
					cb.c_model_matrix = Matrix.Transpose(worldMatrix);
					cb.c_model_view_matrix = Matrix.Transpose(Matrix.Multiply(worldMatrix, camera.ViewMatrix));
					mv = Matrix.Multiply(worldMatrix, camera.ViewMatrix);
					cb.c_normal_matrix = Matrix.Invert(mv);
					cb.c_world_normal_matrix = Matrix.Invert(worldMatrix);

					// キャッシュ
					worldMatrixCache_ = worldMatrix;
					viewMatrixCache_ = camera.ViewMatrix;
				}

				// TODO:カメラによるキャッシュ
				cb.c_view_matrix = Matrix.Transpose(camera.ViewMatrix);
				cb.c_proj_matrix = Matrix.Transpose(camera.ProjectionMatrix);
				cb.c_viewSpaceLightPosition = Renderer.ViewSpaceLightPos;
				cb.c_worldSpaceViewPosition = new Vector4(camera.Position, 1);

				DataStream s = new DataStream(cbTrans.inst_.Buffer, true, true);
				DataBox box = new DataBox(0, 0, s);
				Renderer.D3dCurrentContext.UpdateSubresource(box, cbTrans.buffer_, 0);
				s.Close();
			}


			// ユーザー定義バッファを更新
			var enumulate = activeShader_.ConstantBufferObjects.Where(o => (!o.isSystem_ && o.updateFunc_ != null));
			foreach (var o in enumulate) {
				if (o.updateFunc_(activeShader_, o.inst_)) {
					byte[] data = o.inst_.Buffer;
					DataStream s = new DataStream(data, true, true);
					DataBox box = new DataBox(0, 0, s);
					Renderer.D3dCurrentContext.UpdateSubresource(box, o.buffer_, 0);
					s.Close();
				}
			}
		}


		/// <summary>
		/// マテリアルパラメータセット
		/// </summary>
		/// <param name="mat"></param>
		public
		static void SetMaterialParam(Material mat)
		{
			if ((activeShader_.CBufferBindFlag & (uint)Shader.SystemCBufferFlag.USER) != 0) {
				if (mat.userParams_ != null) {
					DataStream s = new DataStream(mat.userParamSize_, true, true);
					foreach (var v in mat.userParams_) {
						if (v.Value.GetType() == typeof(MaterialParamValue<float>)) {
							s.Write(((MaterialParamValue<float>)v.Value).Value);
						} else if (v.Value.GetType() == typeof(MaterialParamValue<Color4>)) {
							s.Write(((MaterialParamValue<Color4>)v.Value).Value);
						}
					}
					s.Position = 0;
					DataBox box = new DataBox(0, 0, s);
					Renderer.D3dCurrentContext.UpdateSubresource(box, Shader.ConstantBufferDictionary["CB_User"].buffer_, 0);
					s.Close();
				}
			}

#if false
			if ((activeShader_.NeedCBufferFlag & (uint)Shader.ConstantBufferFlag.MATERIAL) != 0) {
				dynamic obj = cbuffer_inst_[(int)Shader.ConstantBufferType.MATERIAL];
				obj.u_material_color = mat.diffuseColor_.ToVector4();
				obj.u_material_param = mat.generalParam_;
				obj.u_specular_color = new Vector3(0, 0, 0);
				byte[] data = cbuffer_inst_[(int)Shader.ConstantBufferType.MATERIAL].Buffer;

				DataStream s = new DataStream(data, true, true);
				DataBox box = new DataBox(0, 0, s);
				Renderer.D3dCurrentContext.UpdateSubresource(box, Shader.ConstantBufferInstList[(int)Shader.ConstantBufferType.MATERIAL], 0);
				s.Close();
			}

			if ((activeShader_.NeedCBufferFlag & (uint)Shader.ConstantBufferFlag.SH_LIGHTING) != 0) {
				dynamic obj = cbuffer_inst_[(int)Shader.ConstantBufferType.SH_LIGHTING];
				if (mat.SHCoef != null) {
					obj.c_sh_coef_0 = mat.SHCoef[0];
					obj.c_sh_coef_1 = mat.SHCoef[1];
					obj.c_sh_coef_2 = mat.SHCoef[2];
					obj.c_sh_coef_3 = mat.SHCoef[3];
					obj.c_sh_coef_4 = mat.SHCoef[4];
					obj.c_sh_coef_5 = mat.SHCoef[5];
					obj.c_sh_coef_6 = mat.SHCoef[6];
					obj.c_sh_coef_7 = mat.SHCoef[7];
					obj.c_sh_coef_8 = mat.SHCoef[8];
					//for (int i = 0; i < 9; i++) {
					//	obj.c_sh_coef[i] = mat.SHCoef[i];
					//}
					byte[] data = cbuffer_inst_[(int)Shader.ConstantBufferType.SH_LIGHTING].Buffer;
					DataStream s = new DataStream(data, true, true);
					DataBox box = new DataBox(0, 0, s);
					Renderer.D3dCurrentContext.UpdateSubresource(box, Shader.ConstantBufferInstList[(int)Shader.ConstantBufferType.SH_LIGHTING], 0);
					s.Close();
				}
			}
#endif
		}


		/// <summary>
		/// キャッシュをクリア
		/// </summary>
		public 
		static void InvalidateCache()
		{
			activeShader_ = null;
		}
	}
}
