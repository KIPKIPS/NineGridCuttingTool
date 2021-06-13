using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;
using Object = UnityEngine.Object;
using System.Collections.Generic;
namespace EditorTools.UI {
    // 格式化贴图尺寸到4的倍数
    public class NineGridCheck {
        //检测的目录,Icon资源和界面预制体引用的贴图资源
        public const string UI_TEXTURE_PATH = "Assets/Textures";
        static List<string> canGridList = new List<string>();
        [MenuItem("Assets/NineGridCheck", false, 103)]
        public static void Main() {
            Object[] objs = Selection.GetFiltered(typeof(Object), SelectionMode.Assets);//过滤选中的对象,这里只筛选Assets类型的
            foreach (Object obj in objs) { //遍历筛选过后选中的内容
                string path = AssetDatabase.GetAssetPath(obj); //获取obj的路径
                string selectedPath = GetSelectedPath(path); //检测路径,按照上面预定义的路径进行审核,返回通过审核的路径
                if (string.IsNullOrEmpty(selectedPath) == true) {
                    return;
                }
                if (selectedPath.Contains(".png") == true) { //选择的路径包含了.png字段
                    //FileMode(selectedPath); //文件模式
                } else {
                    FolderMode(selectedPath);//文件夹
                }
            }
        }
        private static void FolderMode(string folderPath) {
            //Debug.Log(PixelDifferenceDetection(new Color32(255,255,255,255),new Color32(255,255,255,254)));
            canGridList.Clear();
            if (folderPath.Contains(UI_TEXTURE_PATH) == true) {
                string[] pathList = GetAssetPaths(folderPath);
                Texture2D texture;
                TextureImporter importer;
                foreach (string path in pathList) {
                    texture = AssetDatabase.LoadAssetAtPath(path, typeof(Texture2D)) as Texture2D;
                    importer = AssetImporter.GetAtPath(path) as TextureImporter;//获取TextureImporter
                    if (importer == null) {  //不是图片资源就报错,找不到TextureImporter
                        Debug.LogError("发现不是图片的资源, 资源路径 = " + path);
                        break;
                    } else {
                        bool canGrid = ExitNineGridTexture(texture);
                        if (canGrid) {
                            canGridList.Add(texture.name);
                        }
                    }
                }
            }
            if (canGridList.Count > 0) {
                Debug.LogWarning("该路径图集存在可九宫的贴图" + folderPath);
            } else {
                Debug.Log("Awesome job !");
            }
        }
        public static bool CalculateClippableArea(Texture2D sourceTexture) {
            Color32[] sourcePixels = sourceTexture.GetPixels32();
            int width = sourceTexture.width; //贴图宽高
            int height = sourceTexture.height;
            Dictionary<string, int> rowSectionDict = new Dictionary<string, int>();
            string indexStr = "";
            for (int h = 0; h < height; h++) {
                int rowLeft = 0;
                int rowRight = 0;
                Debug.Log("-------------------");
                for (int w = 0; w < width; w++) {
                    Color32 leftPixel = sourcePixels[h * width + rowLeft]; //h * width + w
                    Color32 rightPixel = sourcePixels[h * width + rowRight];
                    if (PixelDifferenceDetection(leftPixel, rightPixel)) { //色差检测
                        rowRight++;
                    } else {
                        //Debug.Log(rowLeft + " " + rowRight);
                        indexStr = rowLeft + "_" + rowRight;
                        if (!rowSectionDict.ContainsKey(indexStr)) {
                            rowSectionDict.Add(indexStr, 1);
                        } else {
                            rowSectionDict[indexStr]++;
                        }
                        rowLeft = w;
                        rowRight = w;
                    }
                    if (w == width - 1) {
                        //Debug.Log((rowLeft - 1) + " " + w);
                        indexStr = (rowLeft - 1) + "_" + width;
                        if (!rowSectionDict.ContainsKey(indexStr)) {
                            rowSectionDict.Add(indexStr, 1);
                        } else {
                            rowSectionDict[indexStr]++;
                        }
                    }
                }
            }
            foreach (KeyValuePair<string, int> kvp in rowSectionDict) {
                Debug.Log(kvp.Key + " " + kvp.Value);
            }
            return false;
        }
        public static bool ExitNineGridTexture(Texture2D sourceTexture) {
            Color32[] sourcePixels = sourceTexture.GetPixels32();
            int width = sourceTexture.width; //贴图宽高
            int height = sourceTexture.height;
            //横向计算的可合并区间
            int xLeft = -1;
            int xRight = width;
            List<int[]> overlapSectionList = new List<int[]>();
            for (int h = 0; h < height; h++) {
                int rowLeft = 0;
                int rowRight = 0;
                overlapSectionList.Clear();
                for (int w = 0; w < width; w++) { //自左向右搜索每一行的可归并像素区间
                    Color32 leftPixel = sourcePixels[h * width + rowLeft]; //h * width + w
                    Color32 rightPixel = sourcePixels[h * width + rowRight];
                    if (PixelDifferenceDetection(leftPixel, rightPixel)) { //色差检测
                        rowRight++;
                    } else {
                        if (rowLeft < rowRight) {
                            //存在重叠区域
                            if (rowLeft > xLeft && rowRight < xRight) {
                                int[] section = new int[2];
                                section[0] = rowLeft;
                                section[1] = rowRight;
                                overlapSectionList.Add(section);
                            }
                        }
                        rowLeft = w;
                        rowRight = w;
                    }
                }
                if (overlapSectionList.Count > 0) {
                    int maxSection = 0;
                    int left = -1;
                    int right = width + 1;
                    foreach (int[] section in overlapSectionList) {
                        if (section[1] - section[0] > maxSection) {
                            maxSection = section[1] - section[0];
                            left = section[0];
                            right = section[1];
                        }
                    }
                    if (left >= 0 && right <= width) {
                        if (left >= xLeft) {
                            xLeft = left;
                        }
                        if (right <= xRight) {
                            xRight = right;
                        }
                    }
                }
                // if ("world_relic_page2_elem3" == sourceTexture.name) {
                //     Debug.Log("xl: "+xLeft+" xr: "+xRight);  
                // }
            }
            //纵向的可合并区间
            int yBottom = -1;
            int yTop = height;
            for (int w = 0; w < width; w++) {
                int columnBottom = 0;
                int columnTop = 0;
                overlapSectionList.Clear();
                for (int h = 0; h < height - 1; h++) { //自底向上搜索每一列的可归并像素区间
                    Color32 bottomPixel = sourcePixels[columnBottom * width + w]; //h * width + w
                    Color32 topPixel = sourcePixels[columnTop * width + w];
                    if (PixelDifferenceDetection(bottomPixel, topPixel)) { //色差检测
                        columnTop++;
                    } else {
                        if (columnBottom < columnTop) {
                            //存在重叠区域
                            if (columnBottom > yBottom && columnTop < yTop) {
                                int[] section = new int[2];
                                section[0] = columnBottom;
                                section[1] = columnTop;
                                overlapSectionList.Add(section);
                            }
                        }
                        columnBottom = h;
                        columnTop = h;
                    }
                }
                if (overlapSectionList.Count > 0) {
                    int maxSection = 0;
                    int bottom = -1;
                    int top = height + 1;
                    foreach (int[] section in overlapSectionList) {
                        if (section[1] - section[0] > maxSection) {
                            maxSection = section[1] - section[0];
                            bottom = section[0];
                            top = section[1];
                        }
                    }
                    if (bottom >= 0 && top <= height) {
                        if (bottom >= yBottom) {
                            yBottom = bottom;
                        }
                        if (top <= yTop) {
                            yTop = top;
                        }
                    }
                }
                // if ("62" == sourceTexture.name) {
                //     Debug.Log(" yb: " + yBottom + " yt: " + yTop);
                // }
            }
            bool canGrid = false;
            int xDis = xRight - xLeft;
            xDis = xDis > width ? width : xDis;
            int yDis = yTop - yBottom;
            yDis = yDis > height ? height : yDis;
            //矩形框计算
            int borderL = Mathf.Clamp(xLeft, 0, width);
            int borderT = Mathf.Clamp(height - yTop, 0, height);
            int borderR = Mathf.Clamp(width - xRight, 0, width);
            int borderB = Mathf.Clamp(yBottom, 0, height);
            int areaLR = (borderL + borderR) * (height - borderT - borderB);//可裁剪面积
            int areaBT = (borderT + borderB) * (width - borderL - borderR);
            int middleArea = (height - borderT - borderB) * (width - borderL - borderR);
            int maskArea = middleArea + Mathf.Max(areaLR, areaBT);
            if ("62" == sourceTexture.name) {
                Debug.Log("area :" + sourcePixels.Length + " mask area : " + maskArea);
                Debug.Log("xl: " + xLeft + " xr: " + xRight + " yb: " + yBottom + " yt: " + yTop);
                Debug.Log("L: " + borderL + " T: " + borderT + " R : " + borderR + " B: " + borderB);
            }
            if (maskArea >= areaThresholdPerecent * sourcePixels.Length && maskArea < sourcePixels.Length && maskArea >= areaThreshold) {
                canGrid = !AllTransparent(sourcePixels, borderL, borderT, borderR, borderB, width, height);
                // if ("world_relic_page2_elem3" == sourceTexture.name){
                //     Debug.Log(AllTransparent(sourcePixels,borderL,borderT,borderR,borderB,width,height));
                // }
            }
            if (canGrid) {
                string red = "ff0000";
                string prefixStr = AddColor(sourceTexture.name, red);
                Debug.Log(prefixStr + ": Border : L " + AddColor(borderL) + " T " + AddColor(borderT) + " R " + AddColor(borderR) + " B " + AddColor(borderB));
                //Debug.Log("area :" + sourcePixels.Length + " mask area : " + maskArea);
            }
            return canGrid;
        }
        static bool AllTransparent(Color32[] pixels, int borderL, int borderT, int borderR, int borderB, int width, int height) {
            //中心区域
            for (int y = borderB; y < height - borderT; y++) {
                for (int x = borderL; x < width - borderR; x++) {
                    if (pixels[y * width + x].a >= 0) {
                        return false;
                    }
                }
            }
            //左裁剪区域
            for (int y = borderB; y < height - borderT; y++) {
                for (int x = 0; x < borderL; x++) {
                    if (pixels[y * width + x].a >= 0) {
                        return false;
                    }
                }
            }
            //右裁剪区域
            for (int y = borderB; y < height - borderT; y++) {
                for (int x = width - borderR; x < width; x++) {
                    if (pixels[y * width + x].a >= 0) {
                        return false;
                    }
                }
            }
            //底裁剪区域
            for (int y = 0; y < borderB; y++) {
                for (int x = borderL; x < width - borderR; x++) {
                    if (pixels[y * width + x].a >= 0) {
                        return false;
                    }
                }
            }
            //上裁剪区域
            for (int y = height - borderT; y < height; y++) {
                for (int x = borderL; x < width - borderR; x++) {
                    if (pixels[y * width + x].a >= 0) {
                        return false;
                    }
                }
            }
            return true;
        }
        const float areaThresholdPerecent = 0.3f;//可裁剪面积占比阀值
        const int areaThreshold = 400;
        const float Chromatic_Aberration = 0.1f; //色差
        enum ColorSpace {
            RGB = 1,
            HSV = 2,
            LAB = 3,
        }
        static bool PixelDifferenceDetection(Color32 a, Color32 b) {
            //return a.a == b.a && a.r == b.r && a.g == b.g && a.b == b.b;
            float aH, aS, aV;
            Color.RGBToHSV(a, out aH, out aS, out aV);
            HSV hsv1 = new HSV(aH, aS, aV);
            float bH, bS, bV;
            Color.RGBToHSV(b, out bH, out bS, out bV);
            HSV hsv2 = new HSV(bH, bS, bV);
            float dis = HSV.EuclideanDistanceSquare(hsv1, hsv2);
            return dis < Chromatic_Aberration * Chromatic_Aberration && a.a == b.a;
        }
        // public float LAB() {
        //     long rmean = ( (long)e1.r + (long)e2.r ) / 2;
        //     long r = (long)e1.r - (long)e2.r;
        //     long g = (long)e1.g - (long)e2.g;
        //     long b = (long)e1.b - (long)e2.b;
        //     return sqrt((((512+rmean)*r*r)>>8) + 4*g*g + (((767-rmean)*b*b)>>8));
        //     return 1;
        // }
        public class HSV {
            public float H;
            public float S;
            public float V;
            public HSV(float h, float s, float v) {
                H = h; S = s; V = v;
            }
            private static float R = 100;
            private static float angle = 30;
            private static float h = R * Mathf.Cos(angle / 180 * Mathf.PI);
            private static float r = R * Mathf.Sin(angle / 180 * Mathf.PI);
            public static float EuclideanDistanceSquare(Color32 a, Color32 b) {
                float aH, aS, aV;
                Color.RGBToHSV(a, out aH, out aS, out aV);
                HSV hsv1 = new HSV(aH, aS, aV);
                float bH, bS, bV;
                Color.RGBToHSV(b, out bH, out bS, out bV);
                HSV hsv2 = new HSV(bH, bS, bV);
                return EuclideanDistanceSquare(hsv1, hsv2);
            }
            public static float EuclideanDistanceSquare(HSV hsv1, HSV hsv2) {
                float x1 = r * hsv1.V * hsv1.S * Mathf.Cos(hsv1.H / 180 * Mathf.PI);
                float y1 = r * hsv1.V * hsv1.S * Mathf.Sin(hsv1.H / 180 * Mathf.PI);
                float z1 = h * (1 - hsv1.V);
                float x2 = r * hsv2.V * hsv2.S * Mathf.Cos(hsv2.H / 180 * Mathf.PI);
                float y2 = r * hsv2.V * hsv2.S * Mathf.Sin(hsv2.H / 180 * Mathf.PI);
                float z2 = h * (1 - hsv2.V);
                float dx = x1 - x2;
                float dy = y1 - y2;
                float dz = z1 - z2;
                return dx * dx + dy * dy + dz * dz;
            }
        }
        private static string[] GetAssetPaths(string folderPath) {
            string[] result = Directory.GetFiles(folderPath, "*.*", SearchOption.TopDirectoryOnly).Where<string>(s => s.Contains(".meta") == false).ToArray<string>();
            for (int i = 0; i < result.Length; i++) { //通过过滤的文件列表,此时只包含图片资源
                result[i] = result[i].Replace(@"\", @"/");//把所有的'\'替换成'/'
            }
            return result;
        }
        private static string GetSelectedPath(string path) {
            if (path.Contains(UI_TEXTURE_PATH) == false) { //路径不包含UI预设引用贴图路径,检测不通过
                Debug.LogError("选择的路径是： " + path + " 错误，请选择Assets/Things/Textures/UI路径下的目录");
                return string.Empty;
            }
            return path;
        }
        static string AddColor(string str) {
            return AddColor(str, "ff0000");
        }
        static string AddColor(int num) {
            return AddColor(num, "ff0000");
        }
        static string AddColor(string str, string color) {
            return "<color=#" + color + ">" + str + "</color>";
        }
        static string AddColor(int num, string color) {
            return "<color=#" + color + ">" + num.ToString() + "</color>";
        }
    }
}