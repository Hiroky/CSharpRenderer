using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Globalization;
using SlimDX;

namespace Lib.Ext
{
    [Serializable]
    class ObjFileLoader : MeshLoader
    {

        public ObjFileLoader()
        {
		}


		/// <summary>
		/// マテリアルの読み込み
		/// </summary>
        public void LoadMaterials(String fileName)
        {
            CultureInfo ci = new CultureInfo("en-US", false);

            try
            {
                using (StreamReader sr = new StreamReader(fileName))
                {
                    bool end = false;

					MaterialObject currentObj = null;

                    while (!end)
                    {
                        if (sr.EndOfStream)
                        {
                            end = true;
                            continue;
                        }

                        String line = sr.ReadLine().Trim();
                        if (line.StartsWith("#"))
                        {
                            continue;
                        }

                        if (line.Length < 1)
                        {
                            continue;
                        }

                        if (line.StartsWith("newmtl "))
                        {
                            var tokens = line.Split(new String[] { " " }, StringSplitOptions.RemoveEmptyEntries);

							currentObj = new MaterialObject();
							currentObj.ID = tokens[1];
							m_materialList.Add(currentObj);
                        }

                        if (line.StartsWith("map_Kd "))
                        {
                            var tokens = line.Split(new String[] { " " }, StringSplitOptions.RemoveEmptyEntries);

                            String texture = tokens[1];
							if (!m_textureList.ContainsKey(texture)) {
								m_textureList.Add(texture, texture);
							}
							currentObj.textureID.Add(texture);
                        }

                        if (line.StartsWith("map_bump "))
                        {
                            var tokens = line.Split(new String[] { " " }, StringSplitOptions.RemoveEmptyEntries);

                            String texture = tokens[1];
							if (!m_textureList.ContainsKey(texture)) {
								m_textureList.Add(texture, texture);
							}
							currentObj.textureID.Add(texture);
						}

                    }
                }
            }
            catch (IOException e)
            {
                Console.WriteLine("The file could not be read:");
                Console.WriteLine(e.Message);
            }
        }


		/// <summary>
		/// 読み込み
		/// </summary>
		/// <param name="fileName"></param>
		public override void Load(String fileName, bool calcTangent = false)
        {
            CultureInfo ci = new CultureInfo("en-US", false);

            try
            {
                using (StreamReader sr = new StreamReader(fileName))
                {
                    bool end = false;
					DrawSubset currentSubset = null;

                    while (!end)
                    {
                        if (sr.EndOfStream)
                        {
                            end = true;
                            continue;
                        }

                        String line = sr.ReadLine().Trim();
                        if (line.StartsWith("#"))
                        {
                            continue;
                        }

                        if (line.Length < 1)
                        {
                            continue;
                        }

                        if (line.StartsWith("usemtl "))
                        {
                            var tokens = line.Split(new String[] { " " }, StringSplitOptions.RemoveEmptyEntries);
							if (m_subsetList.Count == 0 || m_subsetList[m_subsetList.Count - 1].material != tokens[1]) {
								if (currentSubset != null) {
									currentSubset.endIndex = m_Indices.Count;
								}
								currentSubset = new DrawSubset();
								currentSubset.material = tokens[1];
								if (m_Indices.Count > 0) {
									currentSubset.startIndex = m_Indices.Count;
								}
								m_subsetList.Add(currentSubset);
							}
                        }

                        if (line.StartsWith("v "))
                        {
                            var tokens = line.Split(new String[] { " " }, StringSplitOptions.RemoveEmptyEntries);
                            MyVertex v = new MyVertex();
                            v.x = Convert.ToSingle(tokens[1], ci) / 100.0f;
                            v.y = Convert.ToSingle(tokens[2], ci) / 100.0f;
                            v.z = Convert.ToSingle(tokens[3], ci) / 100.0f;
                            m_objVertices.Add(v);

                            m_BoundingBoxMax = Vector3.Maximize(m_BoundingBoxMax, new Vector3(v.x, v.y, v.z));
                            m_BoundingBoxMin = Vector3.Minimize(m_BoundingBoxMin, new Vector3(v.x, v.y, v.z));
                        }

                        if (line.StartsWith("vn "))
                        {
                            var tokens = line.Split(new String[] { " " }, StringSplitOptions.RemoveEmptyEntries);
                            Vector3 v = new Vector3();
                            v.X = Convert.ToSingle(tokens[1], ci);
                            v.Y = Convert.ToSingle(tokens[2], ci);
                            v.Z = Convert.ToSingle(tokens[3], ci);
                            m_Normals.Add(v);
                        }

                        if (line.StartsWith("vt "))
                        {
                            var tokens = line.Split(new String[] { " " }, StringSplitOptions.RemoveEmptyEntries);
                            Vector2 v = new Vector2();
                            v.X = Convert.ToSingle(tokens[1], ci);
                            v.Y = Convert.ToSingle(tokens[2], ci);
                            m_TexCoords.Add(v);
                        }

                        if (line.StartsWith("f "))
                        {
                            var tokens = line.Split(new String[] { " " }, StringSplitOptions.RemoveEmptyEntries);

                            if (tokens.Length > 4)
                            {
                                Int32[] tmpArray = new Int32[tokens.Length - 1];
                                for (int i = 0; i < tokens.Length - 1; ++i)
                                {
                                    var splitTokens = tokens[i + 1].Split(new String[] { "/" }, StringSplitOptions.RemoveEmptyEntries);
                                    tmpArray[i] = Convert.ToInt32(splitTokens[0]);
                                    Int32 normalIndex = Convert.ToInt32(splitTokens[2]);
                                    Int32 uvIndex = Convert.ToInt32(splitTokens[1]);

                                    MyVertex objVertex = m_objVertices[tmpArray[i] - 1];

                                    MyVertex v = new MyVertex();
                                    //v = objVertex;
									v.x = objVertex.x;
									v.y = objVertex.y;
									v.z = objVertex.z;
									v.nx = objVertex.nx;
									v.ny = objVertex.ny;
									v.nz = objVertex.nz;

                                    v.nx += m_Normals[normalIndex - 1].X;
                                    v.ny += m_Normals[normalIndex - 1].Y;
                                    v.nz += m_Normals[normalIndex - 1].Z;

                                    v.u = m_TexCoords[uvIndex - 1].X;
                                    v.v = 1.0f - m_TexCoords[uvIndex - 1].Y;

                                    m_Vertices.Add(v);
                                    tmpArray[i] = m_Vertices.Count;
                                }

                                m_Indices.Add(tmpArray[0] - 1);
                                m_Indices.Add(tmpArray[1] - 1);
                                m_Indices.Add(tmpArray[2] - 1);
                                m_Indices.Add(tmpArray[2] - 1);
                                m_Indices.Add(tmpArray[3] - 1);
                                m_Indices.Add(tmpArray[0] - 1);
                            }
                            else
                            {
                                for (int i = 0; i < 3; ++i)
                                {
                                    var splitTokens = tokens[i + 1].Split(new String[] { "/" }, StringSplitOptions.RemoveEmptyEntries);
                                    Int32 index = Convert.ToInt32(splitTokens[0]) - 1;
                                    Int32 normalIndex = Convert.ToInt32(splitTokens[2]);
                                    Int32 uvIndex = Convert.ToInt32(splitTokens[1]);

                                    MyVertex objVertex = m_objVertices[index];

                                    MyVertex v = new MyVertex();
                                    //v = objVertex;
									v.x = objVertex.x;
									v.y = objVertex.y;
									v.z = objVertex.z;
									v.nx = objVertex.nx;
									v.ny = objVertex.ny;
									v.nz = objVertex.nz;

                                    v.nx += m_Normals[normalIndex - 1].X;
                                    v.ny += m_Normals[normalIndex - 1].Y;
                                    v.nz += m_Normals[normalIndex - 1].Z;
                                    v.u = m_TexCoords[uvIndex - 1].X;
                                    v.v = 1.0f - m_TexCoords[uvIndex - 1].Y;

                                    m_Vertices.Add(v);
                                    m_Indices.Add(m_Vertices.Count - 1);
                                }
                            }
                        }
                    }
                    if (m_Indices.Count > 0)
                    {
						currentSubset.endIndex = m_Indices.Count;
                    }

                }

                for (int i = 0; i < m_Vertices.Count; ++i)
                {
                    MyVertex vertexCopy = m_Vertices[i];
                    Vector3 normal = new Vector3(vertexCopy.nx, vertexCopy.ny, vertexCopy.nz);
                    normal.Normalize();
                    vertexCopy.nx = normal[0];
                    vertexCopy.ny = normal[1];
                    vertexCopy.nz = normal[2];

                    m_Vertices[i] = vertexCopy;
                }

				// tangent等を求める
				if (calcTangent) {
					ComputeTangentFrame();
				}
            }
            catch (IOException e)
            {
                Console.WriteLine("The file could not be read:");
                Console.WriteLine(e.Message);
            }
        }
    }
}
