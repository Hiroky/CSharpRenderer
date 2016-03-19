using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.ComponentModel;

namespace Renderer
{
	/// <summary>
	/// 表示
	/// </summary>
	public class ViewItems
	{
		public enum Type
		{
			Result,
			GBuffer0,
			GBuffer1,
			GBuffer2,
			ShadowMap,
			LuminanceAvg,
		}

		public EnumItem[] items_;
		public EnumItem[] Items { get { return items_; } }

		public ViewItems()
		{
			items_ = new EnumItem[] {
				new EnumItem((int)Type.Result, "最終出力"),
				new EnumItem((int)Type.GBuffer0, "GBuffer0"),
				new EnumItem((int)Type.GBuffer1, "GBuffer1"),
				new EnumItem((int)Type.GBuffer2, "GBuffer2"),
				new EnumItem((int)Type.ShadowMap, "ShadowMap"),
				new EnumItem((int)Type.LuminanceAvg, "LuminaceAvg"),
			};
		}
	}


	/// <summary>
	/// タイルドディファードのデバッグ表示アイテム
	/// </summary>
	public class TiledRenderItems
	{
		public enum Type
		{
			Result,
			NoUseTiled,
			LightCount,
		}

		public EnumItem[] items_;
		public EnumItem[] Items { get { return items_; } }

		public TiledRenderItems()
		{
			items_ = new EnumItem[] {
				new EnumItem((int)Type.Result, "TiledDeffered"),
				new EnumItem((int)Type.NoUseTiled, "Deffered"),
				new EnumItem((int)Type.LightCount, "LightNumPerTile"),
			};
		}
	}

}
