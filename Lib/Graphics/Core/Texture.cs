using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Runtime.InteropServices;

using SlimDX;
using SlimDX.Direct3D11;
using SlimDX.DXGI;
using D3D11Resource = SlimDX.Direct3D11.Resource;

namespace Lib
{
	/// <summary>
	/// テクスチャ
	/// </summary>
	public class Texture : IShaderView, IDisposable
	{
		/// <summary>
		/// 初期化用構造体
		/// </summary>
		public struct InitDesc
		{
			public int width, height, depth;
			public Format format;
			public int mips;
			public TextureBuffer.BindFlag bindFlag;
			public byte[][][] initData;
			public DataStream initStream;
			public ResourceOptionFlags optionFlags;
		}

		TextureBuffer buffer_;
		RenderState.TextureAddressing addressingModeU_ = RenderState.TextureAddressing.Wrap;
		RenderState.TextureAddressing addressingModeV_ = RenderState.TextureAddressing.Wrap;


		public int Width { get { return buffer_.Width; } }
		public int Height { get { return buffer_.Height; } }
		public Format ImageFormat { get { return buffer_.ImageFormat; } }
		public D3D11Resource TextureResource { get { return buffer_.Resource; } }
		public ShaderResourceView ShaderResourceView { get { return buffer_.ShaderResourceView; } }
		public RenderTargetView RenderTargetView { get { return buffer_.RenderTargetView; } }
		public DepthStencilView DepthStencilView { get { return buffer_.DepthStencilView; } }
		public UnorderedAccessView UnorderedAccessView { get { return buffer_.UnorderedAccessView; } }
		public RenderTargetView[] ArrayRenderTargetView { get { return buffer_.ArryaRenderTargetView; } }
		public RenderState.TextureAddressing AddressingModeU { get { return addressingModeU_; } set { addressingModeU_ = value; } }
		public RenderState.TextureAddressing AddressingModeV { get { return addressingModeV_; } set { addressingModeV_ = value; } }

		public Texture()
		{
		}

		/// <summary>
		/// ファイルから初期化
		/// </summary>
		/// <param name="file_name"></param>
		public Texture(string file_name)
		{
			Initialize(file_name);
		}

		/// <summary>
		/// ポインタから初期化
		/// </summary>
		/// <param name="bin"></param>
		public Texture(IntPtr ptr)
		{
			Initialize(ptr);
		}


		/// <summary>
		/// パラメータから初期化
		/// </summary>
		/// <param name="desc"></param>
		public Texture(InitDesc desc)
		{
			Initialize(desc);
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="file_name"></param>
		public void Initialize(string file_name)
		{
			buffer_ = new TextureBuffer(file_name);
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="ptr"></param>
		public void Initialize(IntPtr ptr)
		{
			buffer_ = new TextureBuffer(ptr);
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="desc"></param>
		public void Initialize(InitDesc desc)
		{
			TextureBuffer.BufferDesc buffer_desc = new TextureBuffer.BufferDesc {
				width = desc.width,
				height = desc.height,
				depth = desc.depth,
				format = desc.format,
				bindFlag = desc.bindFlag,
				optionFlags = desc.optionFlags,
				mips = desc.mips,
				initData = desc.initData,
				initStream = desc.initStream,
			};
			buffer_ = new TextureBuffer(buffer_desc);
		}


		/// <summary>
		/// 廃棄
		/// </summary>
		public void Dispose()
		{
			if (buffer_ != null) {
				buffer_.Dispose();
				buffer_ = null;
			}
		}


		/// <summary>
		/// 
		/// </summary>
		/// <param name="slot"></param>
		/// <returns></returns>
		public byte[] GetBufferData(int slot)
		{
			return buffer_.GetBufferData(slot);
		}


		/// <summary>
		/// 
		/// </summary>
		/// <param name="slot"></param>
		/// <returns></returns>
		public DataStream GetBufferDataStream(int slot)
		{
			return buffer_.GetBufferDataStream(slot);
		}


		/// <summary>
		/// テクスチャデータをファイルに保存
		/// </summary>
		/// <param name="fileName"></param>
		public void SaveFile(string fileName)
		{
			if (buffer_.Resource is Texture2D) {
				Texture2D.SaveTextureToFile(GraphicsCore.D3D11ImmediateContext, buffer_.Resource, ImageFileFormat.Dds, fileName);
			} else if (buffer_.Resource is Texture3D) {
				Texture3D.SaveTextureToFile(GraphicsCore.D3D11ImmediateContext, buffer_.Resource, ImageFileFormat.Dds, fileName);
			}
		}
	}
}
