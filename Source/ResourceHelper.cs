using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Reflection;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using DashStyle = System.Drawing.Drawing2D.DashStyle;
using LinearGradientBrush = System.Drawing.Drawing2D.LinearGradientBrush;
using Pen = System.Drawing.Pen;
using Brush = System.Drawing.Brush;
using SolidBrush = System.Drawing.SolidBrush;
using Font = System.Drawing.Font;
using Color = System.Drawing.Color;

namespace HDTool
{
    /// <summary>
    /// Hỗ trợ tạo icon đồ họa cho Ribbon Revit
    /// </summary>
    public static class ResourceHelper
    {
        public static ImageSource CreateLevelCropIcon(int size)
        {
            using (Bitmap bmp = new Bitmap(size, size))
            {
                using (Graphics g = Graphics.FromImage(bmp))
                {
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                    g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
                    g.Clear(System.Drawing.Color.Transparent);

                    float scale = size / 32.0f;

                    // Khung Crop View hình chữ nhật nét đứt
                    using (System.Drawing.Pen cropPen = new System.Drawing.Pen(System.Drawing.Color.FromArgb(148, 163, 184), 1.2f * scale))
                    {
                        cropPen.DashStyle = DashStyle.Dash;
                        g.DrawRectangle(cropPen, 8f * scale, 4f * scale, 16f * scale, 24f * scale);
                    }

                    // Đường Level 2 (Tầng trên)
                    using (System.Drawing.Pen levelPen = new System.Drawing.Pen(System.Drawing.Color.FromArgb(220, 38, 38), 1.8f * scale))
                    {
                        levelPen.DashStyle = DashStyle.DashDot;
                        g.DrawLine(levelPen, 2f * scale, 10f * scale, 29f * scale, 10f * scale);
                    }
                    // Đầu tam giác Level Head trên
                    using (System.Drawing.SolidBrush lvlBrush = new System.Drawing.SolidBrush(System.Drawing.Color.FromArgb(220, 38, 38)))
                    {
                        PointF[] tri = new PointF[] {
                            new PointF(2f * scale, 10f * scale),
                            new PointF(7f * scale, 6.5f * scale),
                            new PointF(7f * scale, 13.5f * scale)
                        };
                        g.FillPolygon(lvlBrush, tri);
                    }

                    // Đường Level 1 (Tầng dưới)
                    using (System.Drawing.Pen levelPen2 = new System.Drawing.Pen(System.Drawing.Color.FromArgb(2, 132, 199), 1.8f * scale))
                    {
                        levelPen2.DashStyle = DashStyle.DashDot;
                        g.DrawLine(levelPen2, 2f * scale, 22f * scale, 29f * scale, 22f * scale);
                    }
                    // Đầu tam giác Level Head dưới
                    using (System.Drawing.SolidBrush lvlBrush2 = new System.Drawing.SolidBrush(System.Drawing.Color.FromArgb(2, 132, 199)))
                    {
                        PointF[] tri2 = new PointF[] {
                            new PointF(2f * scale, 22f * scale),
                            new PointF(7f * scale, 18.5f * scale),
                            new PointF(7f * scale, 25.5f * scale)
                        };
                        g.FillPolygon(lvlBrush2, tri2);
                    }

                    // Mũi tên cam đo căn chỉnh khoảng cách
                    using (System.Drawing.Pen arrowPen = new System.Drawing.Pen(System.Drawing.Color.FromArgb(234, 88, 12), 1.3f * scale))
                    {
                        g.DrawLine(arrowPen, 24f * scale, 10f * scale, 24f * scale, 22f * scale);
                        g.DrawLine(arrowPen, 22f * scale, 10f * scale, 26f * scale, 10f * scale);
                        g.DrawLine(arrowPen, 22f * scale, 22f * scale, 26f * scale, 22f * scale);
                    }
                }

                return ConvertToImageSource(bmp);
            }
        }

        public static ImageSource CreateGridCropIcon(int size)
        {
            using (Bitmap bmp = new Bitmap(size, size))
            {
                using (Graphics g = Graphics.FromImage(bmp))
                {
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                    g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
                    g.Clear(System.Drawing.Color.Transparent);

                    float scale = size / 32.0f;

                    // Khung Crop View hình chữ nhật nét đứt
                    using (System.Drawing.Pen cropPen = new System.Drawing.Pen(System.Drawing.Color.FromArgb(148, 163, 184), 1.2f * scale))
                    {
                        cropPen.DashStyle = DashStyle.Dash;
                        g.DrawRectangle(cropPen, 8f * scale, 8f * scale, 16f * scale, 16f * scale);
                    }

                    // Đường Grid Dọc (Trục 1)
                    using (System.Drawing.Pen gridPenV = new System.Drawing.Pen(System.Drawing.Color.FromArgb(2, 132, 199), 1.6f * scale))
                    {
                        g.DrawLine(gridPenV, 16f * scale, 3f * scale, 16f * scale, 29f * scale);
                    }

                    // Bubble Tròn đỉnh trục dọc (chữ "1")
                    using (System.Drawing.SolidBrush bBg = new System.Drawing.SolidBrush(System.Drawing.Color.White))
                    using (System.Drawing.Pen bBorder = new System.Drawing.Pen(System.Drawing.Color.FromArgb(2, 132, 199), 1.4f * scale))
                    {
                        g.FillEllipse(bBg, 12.5f * scale, 1f * scale, 7f * scale, 7f * scale);
                        g.DrawEllipse(bBorder, 12.5f * scale, 1f * scale, 7f * scale, 7f * scale);
                        if (size >= 32)
                        {
                            using (System.Drawing.Font font = new System.Drawing.Font("Segoe UI", 4.5f, System.Drawing.FontStyle.Bold))
                            using (System.Drawing.SolidBrush textBrush = new System.Drawing.SolidBrush(System.Drawing.Color.FromArgb(2, 132, 199)))
                            using (System.Drawing.StringFormat sf = new System.Drawing.StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                            {
                                g.DrawString("1", font, textBrush, new RectangleF(12.5f * scale, 1f * scale, 7f * scale, 7f * scale), sf);
                            }
                        }
                    }

                    // Đường Grid Ngang (Trục A)
                    using (System.Drawing.Pen gridPenH = new System.Drawing.Pen(System.Drawing.Color.FromArgb(16, 185, 129), 1.6f * scale))
                    {
                        g.DrawLine(gridPenH, 3f * scale, 16f * scale, 29f * scale, 16f * scale);
                    }

                    // Bubble Tròn bên trái trục ngang (chữ "A")
                    using (System.Drawing.SolidBrush bBg = new System.Drawing.SolidBrush(System.Drawing.Color.White))
                    using (System.Drawing.Pen bBorder = new System.Drawing.Pen(System.Drawing.Color.FromArgb(16, 185, 129), 1.4f * scale))
                    {
                        g.FillEllipse(bBg, 1f * scale, 12.5f * scale, 7f * scale, 7f * scale);
                        g.DrawEllipse(bBorder, 1f * scale, 12.5f * scale, 7f * scale, 7f * scale);
                        if (size >= 32)
                        {
                            using (System.Drawing.Font font = new System.Drawing.Font("Segoe UI", 4.5f, System.Drawing.FontStyle.Bold))
                            using (System.Drawing.SolidBrush textBrush = new System.Drawing.SolidBrush(System.Drawing.Color.FromArgb(16, 185, 129)))
                            using (System.Drawing.StringFormat sf = new System.Drawing.StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                            {
                                g.DrawString("A", font, textBrush, new RectangleF(1f * scale, 12.5f * scale, 7f * scale, 7f * scale), sf);
                            }
                        }
                    }

                    // Mũi tên đo căn chỉnh
                    using (System.Drawing.Pen arrowPen = new System.Drawing.Pen(System.Drawing.Color.FromArgb(234, 88, 12), 1.3f * scale))
                    {
                        g.DrawLine(arrowPen, 21f * scale, 16f * scale, 26f * scale, 16f * scale);
                        g.DrawLine(arrowPen, 16f * scale, 21f * scale, 16f * scale, 26f * scale);
                    }
                }

                return ConvertToImageSource(bmp);
            }
        }

        public static ImageSource CreateSmartIcon(int size)
        {
            using (Bitmap bmp = new Bitmap(size, size))
            {
                using (Graphics g = Graphics.FromImage(bmp))
                {
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                    g.Clear(System.Drawing.Color.Transparent);

                    float scale = size / 32.0f;

                    // Vẽ ngôi sao ma thuật hoặc biểu tượng Smart
                    using (System.Drawing.SolidBrush brush = new System.Drawing.SolidBrush(System.Drawing.Color.FromArgb(241, 196, 15)))
                    {
                        PointF[] star = new PointF[] {
                            new PointF(16f * scale, 3f * scale),
                            new PointF(20f * scale, 12f * scale),
                            new PointF(29f * scale, 13f * scale),
                            new PointF(22f * scale, 19f * scale),
                            new PointF(24f * scale, 28f * scale),
                            new PointF(16f * scale, 23f * scale),
                            new PointF(8f * scale, 28f * scale),
                            new PointF(10f * scale, 19f * scale),
                            new PointF(3f * scale, 13f * scale),
                            new PointF(12f * scale, 12f * scale)
                        };
                        g.FillPolygon(brush, star);
                    }

                    using (System.Drawing.Pen border = new System.Drawing.Pen(System.Drawing.Color.FromArgb(211, 84, 0), 1.2f * scale))
                    {
                        PointF[] star = new PointF[] {
                            new PointF(16f * scale, 3f * scale),
                            new PointF(20f * scale, 12f * scale),
                            new PointF(29f * scale, 13f * scale),
                            new PointF(22f * scale, 19f * scale),
                            new PointF(24f * scale, 28f * scale),
                            new PointF(16f * scale, 23f * scale),
                            new PointF(8f * scale, 28f * scale),
                            new PointF(10f * scale, 19f * scale),
                            new PointF(3f * scale, 13f * scale),
                            new PointF(12f * scale, 12f * scale)
                        };
                        g.DrawPolygon(border, star);
                    }
                }

                return ConvertToImageSource(bmp);
            }
        }

        public static ImageSource CreateHDToolMenuIcon(int size) { return CreateHDToolIcon(size); }
        public static ImageSource CreateHDToolIcon(int size)
        {
            using (Bitmap bmp = new Bitmap(size, size))
            {
                using (Graphics g = Graphics.FromImage(bmp))
                {
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                    g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
                    g.Clear(Color.Transparent);

                    float s = size / 32.0f;

                    // 1. Vong tron nen den xanh (#0f172a, vien #0284c7)
                    using (SolidBrush bgBrush = new SolidBrush(Color.FromArgb(15, 23, 42)))
                    using (Pen borderPen = new Pen(Color.FromArgb(2, 132, 199), 2.0f * s))
                    {
                        g.FillEllipse(bgBrush, 2f * s, 2f * s, 28f * s, 28f * s);
                        g.DrawEllipse(borderPen, 2f * s, 2f * s, 28f * s, 28f * s);
                    }

                    // 2. Chu HD mau xanh sky (#38bdf8)
                    using (Font fontHd = new Font("Segoe UI", (size >= 32) ? 8.0f * s : 6.0f * s, System.Drawing.FontStyle.Bold))
                    using (SolidBrush hdBrush = new SolidBrush(Color.FromArgb(56, 189, 248)))
                    using (StringFormat sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                    {
                        g.DrawString("HD", fontHd, hdBrush, new RectangleF(2f * s, 3f * s, 28f * s, 14f * s), sf);
                    }

                    // 3. Chu TOOL mau vang cam (#f59e0b)
                    using (Font fontTool = new Font("Segoe UI", (size >= 32) ? 6.0f * s : 4.5f * s, System.Drawing.FontStyle.Bold))
                    using (SolidBrush toolBrush = new SolidBrush(Color.FromArgb(245, 158, 11)))
                    using (StringFormat sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                    {
                        g.DrawString("TOOL", fontTool, toolBrush, new RectangleF(2f * s, 15f * s, 28f * s, 12f * s), sf);
                    }
                }

                return ConvertToImageSource(bmp);
            }
        }

        public static ImageSource CreateSheetListIcon(int size)
        {
            using (Bitmap bmp = new Bitmap(size, size))
            {
                using (Graphics g = Graphics.FromImage(bmp))
                {
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                    g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
                    g.Clear(Color.Transparent);

                    float s = size / 32.0f;

                    // 1. To tai lieu trang
                    using (GraphicsPath path = new GraphicsPath())
                    {
                        path.AddLine(8f * s, 2f * s, 20f * s, 2f * s);
                        path.AddLine(20f * s, 2f * s, 26f * s, 8f * s);
                        path.AddLine(26f * s, 8f * s, 26f * s, 28f * s);
                        path.AddLine(26f * s, 28f * s, 24f * s, 30f * s);
                        path.AddLine(24f * s, 30f * s, 8f * s, 30f * s);
                        path.AddLine(8f * s, 30f * s, 6f * s, 28f * s);
                        path.AddLine(6f * s, 28f * s, 6f * s, 4f * s);
                        path.CloseFigure();

                        using (SolidBrush paperBrush = new SolidBrush(Color.White))
                        using (Pen borderPen = new Pen(Color.FromArgb(100, 116, 139), 1.5f * s))
                        {
                            g.FillPath(paperBrush, path);
                            g.DrawPath(borderPen, path);
                        }
                    }

                    // Mep gap (#cbd5e1)
                    PointF[] fold = new PointF[] {
                        new PointF(20f * s, 2f * s),
                        new PointF(26f * s, 8f * s),
                        new PointF(20f * s, 8f * s)
                    };
                    using (SolidBrush foldBrush = new SolidBrush(Color.FromArgb(203, 213, 225)))
                    using (Pen foldPen = new Pen(Color.FromArgb(100, 116, 139), 1.0f * s))
                    {
                        g.FillPolygon(foldBrush, fold);
                        g.DrawPolygon(foldPen, fold);
                    }

                    // 2. Cac dong ke van ban
                    using (SolidBrush blueLine = new SolidBrush(Color.FromArgb(2, 132, 199)))
                    using (SolidBrush grayLine = new SolidBrush(Color.FromArgb(148, 163, 184)))
                    {
                        g.FillRectangle(blueLine, 9f * s, 11f * s, 14f * s, 2f * s);
                        g.FillRectangle(grayLine, 9f * s, 15f * s, 14f * s, 2f * s);
                        g.FillRectangle(grayLine, 9f * s, 19f * s, 14f * s, 2f * s);
                        g.FillRectangle(grayLine, 9f * s, 23f * s, 10f * s, 2f * s);
                    }

                    // 3. Huy hieu tron Excel xanh la (#16a34a) chu X
                    using (SolidBrush excelBg = new SolidBrush(Color.FromArgb(22, 163, 74)))
                    using (Pen whitePen = new Pen(Color.White, 1.2f * s))
                    {
                        g.FillEllipse(excelBg, 17f * s, 17f * s, 12f * s, 12f * s);
                        g.DrawEllipse(whitePen, 17f * s, 17f * s, 12f * s, 12f * s);
                    }

                    using (Font font = new Font("Segoe UI", (size >= 32) ? 7.0f * s : 5.5f * s, System.Drawing.FontStyle.Bold))
                    using (SolidBrush textBrush = new SolidBrush(Color.White))
                    using (StringFormat sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                    {
                        g.DrawString("X", font, textBrush, new RectangleF(17f * s, 17f * s, 12f * s, 12.5f * s), sf);
                    }
                }

                return ConvertToImageSource(bmp);
            }
        }

        /// <summary>
        /// Tạo icon Ribbon cho lệnh Cập Nhật Danh Mục Bản Vẽ từ Excel (với huy hiệu đồng bộ màu cam)
        /// </summary>
        public static ImageSource CreateUpdateSheetListIcon(int size)
        {
            using (Bitmap bmp = new Bitmap(size, size))
            {
                using (Graphics g = Graphics.FromImage(bmp))
                {
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                    g.Clear(Color.Transparent);

                    float s = size / 32.0f;

                    // 1. To tai lieu trang
                    using (GraphicsPath path = new GraphicsPath())
                    {
                        path.AddLine(8f * s, 2f * s, 20f * s, 2f * s);
                        path.AddLine(20f * s, 2f * s, 26f * s, 8f * s);
                        path.AddLine(26f * s, 8f * s, 26f * s, 28f * s);
                        path.AddLine(26f * s, 28f * s, 24f * s, 30f * s);
                        path.AddLine(24f * s, 30f * s, 8f * s, 30f * s);
                        path.AddLine(8f * s, 30f * s, 6f * s, 28f * s);
                        path.AddLine(6f * s, 28f * s, 6f * s, 4f * s);
                        path.CloseFigure();

                        using (SolidBrush paperBrush = new SolidBrush(Color.White))
                        using (Pen borderPen = new Pen(Color.FromArgb(100, 116, 139), 1.5f * s))
                        {
                            g.FillPath(paperBrush, path);
                            g.DrawPath(borderPen, path);
                        }
                    }

                    // Mep gap (#cbd5e1)
                    PointF[] fold = new PointF[] {
                        new PointF(20f * s, 2f * s),
                        new PointF(26f * s, 8f * s),
                        new PointF(20f * s, 8f * s)
                    };
                    using (SolidBrush foldBrush = new SolidBrush(Color.FromArgb(203, 213, 225)))
                    using (Pen foldPen = new Pen(Color.FromArgb(100, 116, 139), 1.0f * s))
                    {
                        g.FillPolygon(foldBrush, fold);
                        g.DrawPolygon(foldPen, fold);
                    }

                    // 2. Mui ten xoay tron xanh (#0284c7)
                    using (Pen bluePen = new Pen(Color.FromArgb(2, 132, 199), 2.0f * s))
                    {
                        bluePen.StartCap = LineCap.Round;
                        g.DrawArc(bluePen, 11f * s, 13f * s, 10f * s, 10f * s, 0, 310);
                    }

                    PointF[] arrow = new PointF[] {
                        new PointF(19f * s, 14f * s),
                        new PointF(23f * s, 17f * s),
                        new PointF(19f * s, 20f * s)
                    };
                    using (SolidBrush blueBrush = new SolidBrush(Color.FromArgb(2, 132, 199)))
                    {
                        g.FillPolygon(blueBrush, arrow);
                    }
                }

                return ConvertToImageSource(bmp);
            }
        }

        public static ImageSource CreateUpdateToolIcon(int size)
        {
            using (Bitmap bmp = new Bitmap(size, size))
            {
                using (Graphics g = Graphics.FromImage(bmp))
                {
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                    g.Clear(Color.Transparent);

                    float s = size / 32.0f;

                    // 1. Vong tron net dut ngoai (#38bdf8)
                    using (Pen dashPen = new Pen(Color.FromArgb(56, 189, 248), 2.0f * s))
                    {
                        dashPen.DashPattern = new float[] { 4f, 2f };
                        g.DrawEllipse(dashPen, 4f * s, 4f * s, 24f * s, 24f * s);
                    }

                    // 2. Cung mui ten xoay tron (#0284c7)
                    using (Pen bluePen = new Pen(Color.FromArgb(2, 132, 199), 2.5f * s))
                    {
                        bluePen.StartCap = LineCap.Round;
                        g.DrawArc(bluePen, 11f * s, 11f * s, 10f * s, 10f * s, 0, 310);
                    }

                    PointF[] arrow = new PointF[] {
                        new PointF(20f * s, 12f * s),
                        new PointF(25f * s, 16f * s),
                        new PointF(20f * s, 20f * s)
                    };
                    using (SolidBrush blueBrush = new SolidBrush(Color.FromArgb(2, 132, 199)))
                    {
                        g.FillPolygon(blueBrush, arrow);
                    }

                    // 3. Cham tron tam mau cam (#f59e0b)
                    using (SolidBrush orangeBrush = new SolidBrush(Color.FromArgb(245, 158, 11)))
                    {
                        g.FillEllipse(orangeBrush, 14f * s, 14f * s, 4f * s, 4f * s);
                    }
                }

                return ConvertToImageSource(bmp);
            }
        }

        public static ImageSource CreateCropAlignCombinedIcon(int size)
        {
            using (Bitmap bmp = new Bitmap(size, size))
            {
                using (Graphics g = Graphics.FromImage(bmp))
                {
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                    g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
                    g.Clear(Color.Transparent);

                    float s = size / 32.0f;

                    // 1. Khung Crop View bo goc nen toi vien net dut
                    using (GraphicsPath cropPath = new GraphicsPath())
                    {
                        float rad = 3f * s;
                        cropPath.AddArc(4f * s, 4f * s, rad * 2, rad * 2, 180, 90);
                        cropPath.AddArc(28f * s - rad * 2, 4f * s, rad * 2, rad * 2, 270, 90);
                        cropPath.AddArc(28f * s - rad * 2, 28f * s - rad * 2, rad * 2, rad * 2, 0, 90);
                        cropPath.AddArc(4f * s, 28f * s - rad * 2, rad * 2, rad * 2, 90, 90);
                        cropPath.CloseFigure();

                        using (SolidBrush bgBrush = new SolidBrush(Color.FromArgb(77, 15, 23, 42)))
                        {
                            g.FillPath(bgBrush, cropPath);
                        }
                        using (Pen cropPen = new Pen(Color.FromArgb(148, 163, 184), 1.5f * s))
                        {
                            cropPen.DashPattern = new float[] { 3f, 2f };
                            g.DrawPath(cropPen, cropPath);
                        }
                    }

                    // 2. Duong cao do do net dut ngang
                    using (Pen redPen = new Pen(Color.FromArgb(239, 68, 68), 2.0f * s))
                    {
                        redPen.DashPattern = new float[] { 4f, 2f };
                        g.DrawLine(redPen, 2f * s, 16f * s, 28f * s, 16f * s);
                    }
                    using (SolidBrush redBrush = new SolidBrush(Color.FromArgb(220, 38, 38)))
                    {
                        PointF[] tri = new PointF[] {
                            new PointF(28f * s, 16f * s),
                            new PointF(23f * s, 12f * s),
                            new PointF(23f * s, 20f * s)
                        };
                        g.FillPolygon(redBrush, tri);
                    }

                    // 3. Hai duong luoi truc xanh dung
                    using (Pen bluePen = new Pen(Color.FromArgb(2, 132, 199), 2.0f * s))
                    {
                        g.DrawLine(bluePen, 10f * s, 2f * s, 10f * s, 30f * s);
                        g.DrawLine(bluePen, 22f * s, 2f * s, 22f * s, 30f * s);
                    }

                    // 4. Hai bong bong tron so 1 va 2 o dinh
                    using (SolidBrush whiteBrush = new SolidBrush(Color.White))
                    using (Pen blueBorder = new Pen(Color.FromArgb(2, 132, 199), 1.5f * s))
                    using (SolidBrush blueText = new SolidBrush(Color.FromArgb(2, 132, 199)))
                    using (Font font = new Font("Segoe UI", (size >= 32) ? 5.5f * s : 4.5f * s, System.Drawing.FontStyle.Bold))
                    using (StringFormat sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                    {
                        g.FillEllipse(whiteBrush, 6f * s, 1f * s, 8f * s, 8f * s);
                        g.DrawEllipse(blueBorder, 6f * s, 1f * s, 8f * s, 8f * s);
                        g.DrawString("1", font, blueText, new RectangleF(6f * s, 1f * s, 8f * s, 8.5f * s), sf);

                        g.FillEllipse(whiteBrush, 18f * s, 1f * s, 8f * s, 8f * s);
                        g.DrawEllipse(blueBorder, 18f * s, 1f * s, 8f * s, 8f * s);
                        g.DrawString("2", font, blueText, new RectangleF(18f * s, 1f * s, 8f * s, 8.5f * s), sf);
                    }

                    // 5. Mui ten cam o day
                    using (Pen orangePen = new Pen(Color.FromArgb(249, 115, 22), 1.5f * s))
                    using (SolidBrush orangeBrush = new SolidBrush(Color.FromArgb(249, 115, 22)))
                    {
                        g.DrawLine(orangePen, 10f * s, 26f * s, 16f * s, 26f * s);
                        PointF[] arrow = new PointF[] {
                            new PointF(19f * s, 26f * s),
                            new PointF(16f * s, 24.5f * s),
                            new PointF(16f * s, 27.5f * s)
                        };
                        g.FillPolygon(orangeBrush, arrow);
                    }
                }

                return ConvertToImageSource(bmp);
            }
        }

        /// <summary>
        /// Tạo icon Ribbon cho lệnh Thống Kê Khối Lượng Ống Nước & Phụ Kiện (Biểu tượng ống nước MEP chuẩn 100%)
        /// </summary>
        public static ImageSource CreateMepQuantityIcon(int size)
        {
            using (Bitmap bmp = new Bitmap(size, size))
            {
                using (Graphics g = Graphics.FromImage(bmp))
                {
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                    g.Clear(Color.Transparent);

                    float s = size / 32.0f;

                    // 1. Than ong ngang va ong dung
                    using (SolidBrush pipeBrush = new SolidBrush(Color.FromArgb(2, 132, 199)))
                    using (Pen pipeBorder = new Pen(Color.FromArgb(3, 105, 161), 1.2f * s))
                    {
                        g.FillRectangle(pipeBrush, 2f * s, 18f * s, 22f * s, 7f * s);
                        g.DrawRectangle(pipeBorder, 2f * s, 18f * s, 22f * s, 7f * s);

                        g.FillRectangle(pipeBrush, 18f * s, 4f * s, 7f * s, 16f * s);
                        g.DrawRectangle(pipeBorder, 18f * s, 4f * s, 7f * s, 16f * s);
                    }

                    // 2. Vien bich ong sang mau
                    using (SolidBrush hlBrush = new SolidBrush(Color.FromArgb(56, 189, 248)))
                    {
                        g.FillRectangle(hlBrush, 1f * s, 17f * s, 2.5f * s, 9f * s);
                        g.FillRectangle(hlBrush, 17f * s, 3f * s, 9f * s, 2.5f * s);
                    }

                    // 3. Giot nuoc xanh cyan o phia tren
                    using (GraphicsPath dropPath = new GraphicsPath())
                    {
                        dropPath.AddLine(12f * s, 4f * s, 9f * s, 10f * s);
                        dropPath.AddArc(9f * s, 7f * s, 6f * s, 6f * s, 150, 240);
                        dropPath.CloseFigure();

                        using (SolidBrush dropBrush = new SolidBrush(Color.FromArgb(6, 182, 212)))
                        using (Pen whitePen = new Pen(Color.White, 1.0f * s))
                        {
                            g.FillPath(dropBrush, dropPath);
                            g.DrawPath(whitePen, dropPath);
                        }
                    }

                    // 4. Bang khoi luong vang cam o goc duoi phai
                    using (SolidBrush badgeBrush = new SolidBrush(Color.FromArgb(245, 158, 11)))
                    using (Pen whitePen = new Pen(Color.White, 1.0f * s))
                    {
                        g.FillRectangle(badgeBrush, 18f * s, 19f * s, 12f * s, 10f * s);
                        g.DrawRectangle(whitePen, 18f * s, 19f * s, 12f * s, 10f * s);
                        g.DrawLine(whitePen, 20f * s, 22f * s, 28f * s, 22f * s);
                        g.DrawLine(whitePen, 20f * s, 25f * s, 28f * s, 25f * s);
                    }
                }

                return ConvertToImageSource(bmp);
            }
        }

        /// <summary>
        /// Tạo icon Ribbon chuyên biệt cho lệnh Soi Sáng Bảng Khối Lượng Trực Tiếp Trên Sheet:
        /// Tờ bản vẽ Sheet kẻ bảng tính + Kính lúp soi chiếu dòng bảng phát tia sáng rực rỡ
        /// </summary>
        public static ImageSource CreatePickTableHighlightIcon(int size)
        {
            using (Bitmap bmp = new Bitmap(size, size))
            {
                using (Graphics g = Graphics.FromImage(bmp))
                {
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                    g.Clear(System.Drawing.Color.Transparent);

                    float scale = size / 32.0f;

                    // 1. Bản vẽ Sheet nền trắng
                    using (System.Drawing.SolidBrush paperBg = new System.Drawing.SolidBrush(System.Drawing.Color.White))
                    using (System.Drawing.Pen paperBorder = new System.Drawing.Pen(System.Drawing.Color.FromArgb(100, 116, 139), 1.2f * scale))
                    {
                        g.FillRectangle(paperBg, 2f * scale, 3f * scale, 20f * scale, 26f * scale);
                        g.DrawRectangle(paperBorder, 2f * scale, 3f * scale, 20f * scale, 26f * scale);
                    }

                    // Header bảng Sheet màu xanh tím
                    using (System.Drawing.SolidBrush headerBrush = new System.Drawing.SolidBrush(System.Drawing.Color.FromArgb(30, 41, 59)))
                    {
                        g.FillRectangle(headerBrush, 2f * scale, 3f * scale, 20f * scale, 6f * scale);
                    }

                    // Các dòng trong bảng Sheet
                    using (System.Drawing.Pen linePen = new System.Drawing.Pen(System.Drawing.Color.FromArgb(203, 213, 225), 1.0f * scale))
                    {
                        g.DrawLine(linePen, 4f * scale, 13f * scale, 20f * scale, 13f * scale);
                        g.DrawLine(linePen, 4f * scale, 17f * scale, 20f * scale, 17f * scale);
                        g.DrawLine(linePen, 4f * scale, 21f * scale, 20f * scale, 21f * scale);
                        g.DrawLine(linePen, 4f * scale, 25f * scale, 20f * scale, 25f * scale);
                    }

                    // Dòng đang được SOI SÁNG (màu đỏ rực rỡ nổi bật)
                    using (System.Drawing.SolidBrush hlRow = new System.Drawing.SolidBrush(System.Drawing.Color.FromArgb(239, 68, 68)))
                    {
                        g.FillRectangle(hlRow, 3f * scale, 14f * scale, 18f * scale, 3f * scale);
                    }

                    // 2. Kính Lúp Soi Sáng (Spotlight Magnifier)
                    // Cán kính lúp
                    using (System.Drawing.Pen handlePen = new System.Drawing.Pen(System.Drawing.Color.FromArgb(51, 65, 85), 3.5f * scale))
                    {
                        handlePen.StartCap = LineCap.Round;
                        handlePen.EndCap = LineCap.Round;
                        g.DrawLine(handlePen, 24f * scale, 24f * scale, 30f * scale, 30f * scale);
                    }

                    // Tròng kính lúp tròn viền vàng cam sáng bóng
                    using (System.Drawing.SolidBrush lensBg = new System.Drawing.SolidBrush(System.Drawing.Color.FromArgb(160, 254, 243, 199)))
                    using (System.Drawing.Pen lensRim = new System.Drawing.Pen(System.Drawing.Color.FromArgb(217, 119, 6), 2.2f * scale))
                    {
                        g.FillEllipse(lensBg, 13f * scale, 10f * scale, 14f * scale, 14f * scale);
                        g.DrawEllipse(lensRim, 13f * scale, 10f * scale, 14f * scale, 14f * scale);
                    }

                    // Tia sáng lấp lánh (Sparkle) phát sáng từ kính lúp
                    using (System.Drawing.SolidBrush sparkBrush = new System.Drawing.SolidBrush(System.Drawing.Color.FromArgb(234, 88, 12)))
                    {
                        PointF[] spark = new PointF[] {
                            new PointF(20f * scale, 7f * scale),
                            new PointF(21f * scale, 10f * scale),
                            new PointF(24f * scale, 11f * scale),
                            new PointF(21f * scale, 12f * scale),
                            new PointF(20f * scale, 15f * scale),
                            new PointF(19f * scale, 12f * scale),
                            new PointF(16f * scale, 11f * scale),
                            new PointF(19f * scale, 10f * scale)
                        };
                        g.FillPolygon(sparkBrush, spark);
                    }
                }

                return ConvertToImageSource(bmp);
            }
        }

        /// <summary>
        /// Tạo icon Ribbon cho lệnh Căn Khớp Tỷ Lệ Bảng Khối Lượng (Fit Scale & Table Grid)
        /// </summary>
        public static ImageSource CreateFitScaleIcon(int size)
        {
            using (Bitmap bmp = new Bitmap(size, size))
            {
                using (Graphics g = Graphics.FromImage(bmp))
                {
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                    g.Clear(Color.Transparent);

                    float s = size / 32.0f;

                    // 1. Khung ngoai
                    using (SolidBrush bgBrush = new SolidBrush(Color.FromArgb(102, 15, 23, 42)))
                    using (Pen borderPen = new Pen(Color.FromArgb(100, 116, 139), 1.5f * s))
                    {
                        g.FillRectangle(bgBrush, 4f * s, 4f * s, 24f * s, 24f * s);
                        g.DrawRectangle(borderPen, 4f * s, 4f * s, 24f * s, 24f * s);
                    }

                    // 2. Thanh tieu de
                    using (SolidBrush titleBrush = new SolidBrush(Color.FromArgb(2, 132, 199)))
                    {
                        g.FillRectangle(titleBrush, 7f * s, 7f * s, 18f * s, 4f * s);
                    }

                    // 3. Khoi luoi o bang tinh
                    using (SolidBrush gridBg = new SolidBrush(Color.FromArgb(30, 41, 59)))
                    using (Pen gridPen = new Pen(Color.FromArgb(51, 65, 85), 1.0f * s))
                    {
                        g.FillRectangle(gridBg, 7f * s, 13f * s, 18f * s, 12f * s);
                        g.DrawRectangle(gridPen, 7f * s, 13f * s, 18f * s, 12f * s);
                        g.DrawLine(gridPen, 13f * s, 13f * s, 13f * s, 25f * s);
                        g.DrawLine(gridPen, 19f * s, 13f * s, 19f * s, 25f * s);
                        g.DrawLine(gridPen, 7f * s, 19f * s, 25f * s, 19f * s);
                    }

                    // 4. Mui ten co gian hai ben
                    using (Pen arrowPen = new Pen(Color.FromArgb(56, 189, 248), 1.5f * s))
                    {
                        g.DrawLine(arrowPen, 2f * s, 16f * s, 6f * s, 16f * s);
                        g.DrawLine(arrowPen, 4f * s, 14f * s, 4f * s, 18f * s);
                        g.DrawLine(arrowPen, 26f * s, 16f * s, 30f * s, 16f * s);
                        g.DrawLine(arrowPen, 28f * s, 14f * s, 28f * s, 18f * s);
                    }
                }

                return ConvertToImageSource(bmp);
            }
        }

        /// <summary>
        /// Tạo icon Ribbon chuyên biệt cho tính năng Khối Lượng Ống Gió HVAC (Tôn mạ kẽm m²):
        /// Ống gió 3D Isometric chuẩn cơ điện MEP, mặt bích TDC nẹp C, gân tăng cứng chéo,
        /// luồng gió xanh ngọc lướt qua và huy hiệu vàng m² diện tích ống.
        /// </summary>
        public static ImageSource CreateDuctQuantityIcon(int size)
        {
            using (Bitmap bmp = new Bitmap(size, size))
            {
                using (Graphics g = Graphics.FromImage(bmp))
                {
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                    g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
                    g.Clear(Color.Transparent);

                    float s = size / 32.0f;

                    // 1. Mat tren ong gio
                    PointF[] topFace = new PointF[] {
                        new PointF(5f * s, 9f * s),
                        new PointF(21f * s, 5f * s),
                        new PointF(27f * s, 9f * s),
                        new PointF(11f * s, 13f * s)
                    };
                    using (SolidBrush topBrush = new SolidBrush(Color.FromArgb(148, 163, 184)))
                    using (Pen borderPen = new Pen(Color.FromArgb(71, 85, 105), 1.0f * s))
                    {
                        g.FillPolygon(topBrush, topFace);
                        g.DrawPolygon(borderPen, topFace);
                    }

                    // 2. Mat trai
                    PointF[] leftFace = new PointF[] {
                        new PointF(5f * s, 9f * s),
                        new PointF(11f * s, 13f * s),
                        new PointF(11f * s, 25f * s),
                        new PointF(5f * s, 21f * s)
                    };
                    using (SolidBrush leftBrush = new SolidBrush(Color.FromArgb(203, 213, 225)))
                    using (Pen borderPen = new Pen(Color.FromArgb(71, 85, 105), 1.0f * s))
                    {
                        g.FillPolygon(leftBrush, leftFace);
                        g.DrawPolygon(borderPen, leftFace);
                    }

                    // 3. Mat phai
                    PointF[] rightFace = new PointF[] {
                        new PointF(11f * s, 13f * s),
                        new PointF(27f * s, 9f * s),
                        new PointF(27f * s, 21f * s),
                        new PointF(11f * s, 25f * s)
                    };
                    using (SolidBrush rightBrush = new SolidBrush(Color.FromArgb(100, 116, 139)))
                    using (Pen borderPen = new Pen(Color.FromArgb(71, 85, 105), 1.0f * s))
                    {
                        g.FillPolygon(rightBrush, rightFace);
                        g.DrawPolygon(borderPen, rightFace);
                    }

                    // 4. Huy hieu tron cam o giua
                    using (SolidBrush badgeBrush = new SolidBrush(Color.FromArgb(245, 158, 11)))
                    using (Pen whitePen = new Pen(Color.White, 1.0f * s))
                    {
                        g.FillEllipse(badgeBrush, 14.5f * s, 12.5f * s, 9f * s, 9f * s);
                        g.DrawEllipse(whitePen, 14.5f * s, 12.5f * s, 9f * s, 9f * s);
                    }

                    using (Font font = new Font("Segoe UI", (size >= 32) ? 5.5f * s : 4.5f * s, System.Drawing.FontStyle.Bold))
                    using (SolidBrush textBrush = new SolidBrush(Color.White))
                    using (StringFormat sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                    {
                        g.DrawString("m²", font, textBrush, new RectangleF(14.5f * s, 12.5f * s, 9f * s, 9.5f * s), sf);
                    }
                }

                return ConvertToImageSource(bmp);
            }
        }

        /// <summary>
        /// Tạo icon Ribbon cho Miệng Gió (Air Diffuser):
        /// Khung miệng gió 4 hướng thổi, nan nhôm định hình và mũi tên hướng gió xanh cyan.
        /// </summary>
        public static ImageSource CreateAirDiffuserIcon(int size)
        {
            using (Bitmap bmp = new Bitmap(size, size))
            {
                using (Graphics g = Graphics.FromImage(bmp))
                {
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                    g.Clear(System.Drawing.Color.Transparent);

                    float scale = size / 32.0f;

                    // Khung viền miệng gió ngoài
                    using (SolidBrush bgBrush = new SolidBrush(System.Drawing.Color.FromArgb(241, 245, 249)))
                    using (Pen borderPen = new Pen(System.Drawing.Color.FromArgb(2, 132, 199), 1.5f * scale))
                    {
                        g.FillRectangle(bgBrush, 3f * scale, 3f * scale, 26f * scale, 26f * scale);
                        g.DrawRectangle(borderPen, 3f * scale, 3f * scale, 26f * scale, 26f * scale);
                    }

                    // Khung nan vuông cấp 2
                    using (Pen pen2 = new Pen(System.Drawing.Color.FromArgb(2, 132, 199), 1.2f * scale))
                    {
                        g.DrawRectangle(pen2, 7f * scale, 7f * scale, 18f * scale, 18f * scale);
                    }

                    // Khung nan vuông cấp 3
                    using (Pen pen3 = new Pen(System.Drawing.Color.FromArgb(2, 132, 199), 1.2f * scale))
                    {
                        g.DrawRectangle(pen3, 11f * scale, 11f * scale, 10f * scale, 10f * scale);
                    }

                    // Tâm khuếch tán gió
                    using (SolidBrush centerBrush = new SolidBrush(System.Drawing.Color.FromArgb(2, 132, 199)))
                    {
                        g.FillEllipse(centerBrush, 14f * scale, 14f * scale, 4f * scale, 4f * scale);
                    }

                    // Mũi tên gió thổi 4 hướng
                    using (Pen arrowPen = new Pen(System.Drawing.Color.FromArgb(6, 182, 212), 1.5f * scale))
                    {
                        arrowPen.StartCap = LineCap.Round; arrowPen.EndCap = LineCap.Round;
                        g.DrawLine(arrowPen, 16f * scale, 7f * scale, 16f * scale, 4f * scale);
                        g.DrawLine(arrowPen, 16f * scale, 25f * scale, 16f * scale, 28f * scale);
                        g.DrawLine(arrowPen, 7f * scale, 16f * scale, 4f * scale, 16f * scale);
                        g.DrawLine(arrowPen, 25f * scale, 16f * scale, 28f * scale, 16f * scale);
                    }
                }

                return ConvertToImageSource(bmp);
            }
        }

        /// <summary>
        /// Tạo icon Ribbon cho Van Gió Điều Chỉnh Lưu Lượng (Duct Damper / VCD / OBD):
        /// Thân van vuông, cánh van xoay chỉnh góc và cần gạt điều khiển màu đỏ nổi bật.
        /// </summary>
        public static ImageSource CreateDuctDamperIcon(int size)
        {
            using (Bitmap bmp = new Bitmap(size, size))
            {
                using (Graphics g = Graphics.FromImage(bmp))
                {
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                    g.Clear(System.Drawing.Color.Transparent);

                    float scale = size / 32.0f;

                    // Thân vỏ van gió kim loại
                    using (SolidBrush bodyBrush = new SolidBrush(System.Drawing.Color.FromArgb(51, 65, 85)))
                    using (Pen rimPen = new Pen(System.Drawing.Color.FromArgb(148, 163, 184), 1.5f * scale))
                    {
                        g.FillRectangle(bodyBrush, 4f * scale, 6f * scale, 24f * scale, 20f * scale);
                        g.DrawRectangle(rimPen, 4f * scale, 6f * scale, 24f * scale, 20f * scale);
                    }

                    // Cánh lá van gió màu vàng cam (Damper Blades)
                    using (Pen bladePen = new Pen(System.Drawing.Color.FromArgb(245, 158, 11), 2.2f * scale))
                    {
                        bladePen.StartCap = LineCap.Round; bladePen.EndCap = LineCap.Round;
                        g.DrawLine(bladePen, 8f * scale, 11f * scale, 24f * scale, 11f * scale);
                        g.DrawLine(bladePen, 8f * scale, 16f * scale, 24f * scale, 16f * scale);
                        g.DrawLine(bladePen, 8f * scale, 21f * scale, 24f * scale, 21f * scale);
                    }

                    // Tay gạt điều chỉnh / Cụm động cơ điều khiển màu đỏ
                    using (SolidBrush knobBrush = new SolidBrush(System.Drawing.Color.FromArgb(239, 68, 68)))
                    using (Pen knobPen = new Pen(System.Drawing.Color.White, 1.0f * scale))
                    using (Pen handlePen = new Pen(System.Drawing.Color.FromArgb(239, 68, 68), 2.5f * scale))
                    {
                        handlePen.StartCap = LineCap.Round; handlePen.EndCap = LineCap.Round;
                        g.FillEllipse(knobBrush, 24f * scale, 13f * scale, 6f * scale, 6f * scale);
                        g.DrawEllipse(knobPen, 24f * scale, 13f * scale, 6f * scale, 6f * scale);
                        g.DrawLine(handlePen, 27f * scale, 16f * scale, 29f * scale, 8f * scale);
                    }
                }

                return ConvertToImageSource(bmp);
            }
        }

        /// <summary>
        /// Tạo icon Ribbon cho Thang Máng Cáp Điện (Cable Tray):
        /// </summary>
        public static ImageSource CreateCableTrayIcon(int size)
        {
            using (Bitmap bmp = new Bitmap(size, size))
            {
                using (Graphics g = Graphics.FromImage(bmp))
                {
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                    g.Clear(System.Drawing.Color.Transparent);

                    float scale = size / 32.0f;

                    // 2 Thanh dọc thang cáp màu cam vàng
                    using (Pen railPen = new Pen(System.Drawing.Color.FromArgb(245, 158, 11), 2.5f * scale))
                    {
                        railPen.StartCap = LineCap.Round; railPen.EndCap = LineCap.Round;
                        g.DrawLine(railPen, 4f * scale, 6f * scale, 28f * scale, 6f * scale);
                        g.DrawLine(railPen, 4f * scale, 26f * scale, 28f * scale, 26f * scale);
                    }

                    // Các bước rungs ngang
                    using (Pen rungPen = new Pen(System.Drawing.Color.FromArgb(245, 158, 11), 1.5f * scale))
                    {
                        g.DrawLine(rungPen, 9f * scale, 6f * scale, 9f * scale, 26f * scale);
                        g.DrawLine(rungPen, 16f * scale, 6f * scale, 16f * scale, 26f * scale);
                        g.DrawLine(rungPen, 23f * scale, 6f * scale, 23f * scale, 26f * scale);
                    }

                    // Tia chớp điện màu vàng tia lửa ở giữa
                    PointF[] bolt = new PointF[]
                    {
                        new PointF(17f * scale, 8f * scale),
                        new PointF(11f * scale, 17f * scale),
                        new PointF(16f * scale, 17f * scale),
                        new PointF(15f * scale, 24f * scale),
                        new PointF(22f * scale, 14f * scale),
                        new PointF(17f * scale, 14f * scale)
                    };
                    using (SolidBrush boltBrush = new SolidBrush(System.Drawing.Color.FromArgb(251, 191, 36)))
                    using (Pen boltPen = new Pen(System.Drawing.Color.FromArgb(217, 119, 6), 1.0f * scale))
                    {
                        g.FillPolygon(boltBrush, bolt);
                        g.DrawPolygon(boltPen, bolt);
                    }
                }

                return ConvertToImageSource(bmp);
            }
        }

        /// <summary>
        /// Tạo icon Ribbon cho Kiểm Tra Va Chạm (Clash Check):
        /// </summary>
        public static ImageSource CreateClashCheckIcon(int size)
        {
            using (Bitmap bmp = new Bitmap(size, size))
            {
                using (Graphics g = Graphics.FromImage(bmp))
                {
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                    g.Clear(System.Drawing.Color.Transparent);

                    float scale = size / 32.0f;

                    // Ống ngang xanh
                    using (SolidBrush b1 = new SolidBrush(System.Drawing.Color.FromArgb(2, 132, 199)))
                    using (Pen p1 = new Pen(System.Drawing.Color.FromArgb(3, 105, 161), 1f * scale))
                    {
                        g.FillRectangle(b1, 2f * scale, 13f * scale, 28f * scale, 6f * scale);
                        g.DrawRectangle(p1, 2f * scale, 13f * scale, 28f * scale, 6f * scale);
                    }

                    // Ống dọc cam
                    using (SolidBrush b2 = new SolidBrush(System.Drawing.Color.FromArgb(245, 158, 11)))
                    using (Pen p2 = new Pen(System.Drawing.Color.FromArgb(217, 119, 6), 1f * scale))
                    {
                        g.FillRectangle(b2, 13f * scale, 2f * scale, 6f * scale, 28f * scale);
                        g.DrawRectangle(p2, 13f * scale, 2f * scale, 6f * scale, 28f * scale);
                    }

                    // Vòng tròn va chạm màu đỏ dấu chấm than
                    using (SolidBrush redB = new SolidBrush(System.Drawing.Color.FromArgb(239, 68, 68)))
                    using (Pen whiteP = new Pen(System.Drawing.Color.White, 1.5f * scale))
                    {
                        g.FillEllipse(redB, 9.5f * scale, 9.5f * scale, 13f * scale, 13f * scale);
                        g.DrawEllipse(whiteP, 9.5f * scale, 9.5f * scale, 13f * scale, 13f * scale);
                        if (size >= 32)
                        {
                            using (Font f = new Font("Segoe UI", 8.0f, FontStyle.Bold))
                            using (SolidBrush wb = new SolidBrush(System.Drawing.Color.White))
                            using (StringFormat sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                            {
                                g.DrawString("!", f, wb, new RectangleF(9.5f * scale, 9.5f * scale, 13f * scale, 13f * scale), sf);
                            }
                        }
                    }
                }

                return ConvertToImageSource(bmp);
            }
        }

        /// <summary>
        /// Tạo icon Ribbon cho In Bản Vẽ Hàng Loạt (Batch Print):
        /// </summary>
        public static ImageSource CreateBatchPrintIcon(int size)
        {
            using (Bitmap bmp = new Bitmap(size, size))
            {
                using (Graphics g = Graphics.FromImage(bmp))
                {
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                    g.Clear(System.Drawing.Color.Transparent);

                    float scale = size / 32.0f;

                    // Thân máy in
                    using (SolidBrush b = new SolidBrush(System.Drawing.Color.FromArgb(30, 41, 59)))
                    using (Pen p = new Pen(System.Drawing.Color.FromArgb(2, 132, 199), 1.5f * scale))
                    {
                        g.FillRectangle(b, 4f * scale, 9f * scale, 24f * scale, 14f * scale);
                        g.DrawRectangle(p, 4f * scale, 9f * scale, 24f * scale, 14f * scale);
                    }

                    // Tờ giấy trên
                    using (SolidBrush paperB = new SolidBrush(System.Drawing.Color.FromArgb(248, 250, 252)))
                    using (Pen paperP = new Pen(System.Drawing.Color.FromArgb(100, 116, 139), 1f * scale))
                    {
                        g.FillRectangle(paperB, 8f * scale, 3f * scale, 16f * scale, 8f * scale);
                        g.DrawRectangle(paperP, 8f * scale, 3f * scale, 16f * scale, 8f * scale);
                    }

                    // Tờ bản vẽ in ra dưới
                    using (SolidBrush outB = new SolidBrush(System.Drawing.Color.White))
                    using (Pen outP = new Pen(System.Drawing.Color.FromArgb(148, 163, 184), 1.2f * scale))
                    {
                        g.FillRectangle(outB, 7f * scale, 17f * scale, 18f * scale, 12f * scale);
                        g.DrawRectangle(outP, 7f * scale, 17f * scale, 18f * scale, 12f * scale);
                        using (Pen lineP = new Pen(System.Drawing.Color.FromArgb(2, 132, 199), 1.5f * scale))
                        {
                            g.DrawLine(lineP, 10f * scale, 21f * scale, 22f * scale, 21f * scale);
                            g.DrawLine(lineP, 10f * scale, 25f * scale, 18f * scale, 25f * scale);
                        }
                    }

                    // Đèn tín hiệu xanh
                    using (SolidBrush ledB = new SolidBrush(System.Drawing.Color.FromArgb(16, 185, 129)))
                    {
                        g.FillEllipse(ledB, 22f * scale, 12f * scale, 3f * scale, 3f * scale);
                    }
                }

                return ConvertToImageSource(bmp);
            }
        }

        /// <summary>
        /// Tạo icon Ribbon cho Đồng Bộ BIM Cloud (BIM Sync):
        /// </summary>
        public static ImageSource CreateBimSyncIcon(int size)
        {
            using (Bitmap bmp = new Bitmap(size, size))
            {
                using (Graphics g = Graphics.FromImage(bmp))
                {
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                    g.Clear(System.Drawing.Color.Transparent);

                    float scale = size / 32.0f;

                    // Đám mây xanh BIM
                    using (GraphicsPath cloud = new GraphicsPath())
                    {
                        cloud.AddArc(6f * scale, 14f * scale, 9f * scale, 9f * scale, 90, 180);
                        cloud.AddArc(11f * scale, 8f * scale, 11f * scale, 11f * scale, 180, 180);
                        cloud.AddArc(19f * scale, 12f * scale, 9f * scale, 9f * scale, 270, 135);
                        cloud.CloseFigure();

                        using (SolidBrush b = new SolidBrush(System.Drawing.Color.FromArgb(2, 132, 199)))
                        using (Pen p = new Pen(System.Drawing.Color.FromArgb(56, 189, 248), 1.5f * scale))
                        {
                            g.FillPath(b, cloud);
                            g.DrawPath(p, cloud);
                        }
                    }

                    // Vòng tròn xoay đồng bộ xanh ngọc
                    using (SolidBrush syncB = new SolidBrush(System.Drawing.Color.FromArgb(15, 23, 42)))
                    using (Pen syncP = new Pen(System.Drawing.Color.FromArgb(16, 185, 129), 1.8f * scale))
                    {
                        g.FillEllipse(syncB, 11f * scale, 17f * scale, 14f * scale, 14f * scale);
                        g.DrawEllipse(syncP, 11f * scale, 17f * scale, 14f * scale, 14f * scale);
                        using (Pen arrow = new Pen(System.Drawing.Color.FromArgb(52, 211, 153), 1.4f * scale))
                        {
                            g.DrawArc(arrow, 13f * scale, 19f * scale, 10f * scale, 10f * scale, 45, 180);
                            g.DrawArc(arrow, 13f * scale, 19f * scale, 10f * scale, 10f * scale, 225, 180);
                        }
                    }
                }

                return ConvertToImageSource(bmp);
            }
        }

        /// <summary>
        /// Tạo icon Ribbon cho Căn Kích Thước Dim (Dimension Align):
        /// </summary>
        public static ImageSource CreateDimensionAlignIcon(int size)
        {
            using (Bitmap bmp = new Bitmap(size, size))
            {
                using (Graphics g = Graphics.FromImage(bmp))
                {
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                    g.Clear(System.Drawing.Color.Transparent);

                    float scale = size / 32.0f;

                    // Thân thước đo màu vàng cam kỹ thuật
                    using (SolidBrush rB = new SolidBrush(System.Drawing.Color.FromArgb(251, 191, 36)))
                    using (Pen rP = new Pen(System.Drawing.Color.FromArgb(217, 119, 6), 1.5f * scale))
                    {
                        g.FillRectangle(rB, 3f * scale, 10f * scale, 26f * scale, 12f * scale);
                        g.DrawRectangle(rP, 3f * scale, 10f * scale, 26f * scale, 12f * scale);
                    }

                    // Vạch chia kích thước mm
                    using (Pen tickP = new Pen(System.Drawing.Color.FromArgb(120, 53, 15), 1.2f * scale))
                    {
                        g.DrawLine(tickP, 7f * scale, 10f * scale, 7f * scale, 15f * scale);
                        g.DrawLine(tickP, 11f * scale, 10f * scale, 11f * scale, 18f * scale);
                        g.DrawLine(tickP, 15f * scale, 10f * scale, 15f * scale, 15f * scale);
                        g.DrawLine(tickP, 19f * scale, 10f * scale, 19f * scale, 18f * scale);
                        g.DrawLine(tickP, 23f * scale, 10f * scale, 23f * scale, 15f * scale);
                    }
                }

                return ConvertToImageSource(bmp);
            }
        }

        /// <summary>
        /// Tạo Icon riêng cho tính năng "Khối Lượng Ống Nước" (Chuyên bóc mét dài ống nước)
        /// </summary>
        public static ImageSource CreatePipeOnlyIcon(int size)
        {
            using (Bitmap bmp = new Bitmap(size, size))
            {
                using (Graphics g = Graphics.FromImage(bmp))
                {
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                    g.Clear(System.Drawing.Color.Transparent);

                    float scale = size / 32.0f;

                    // 1. Thân ống nước chính nằm ngang (Màu xanh dương đậm nét #0284c7)
                    using (LinearGradientBrush pipeGrad = new LinearGradientBrush(
                        new PointF(0, 10f * scale), new PointF(0, 22f * scale),
                        System.Drawing.Color.FromArgb(56, 189, 248), System.Drawing.Color.FromArgb(3, 105, 161)))
                    {
                        g.FillRectangle(pipeGrad, 2f * scale, 11f * scale, 28f * scale, 10f * scale);
                    }

                    // Viền ống
                    using (Pen pipeBorder = new Pen(System.Drawing.Color.FromArgb(2, 60, 115), 1.5f * scale))
                    {
                        g.DrawRectangle(pipeBorder, 2f * scale, 11f * scale, 28f * scale, 10f * scale);
                    }

                    // 2. Hai đầu ống (Vành gờ mặt bích / măng sông)
                    using (SolidBrush flangeB = new SolidBrush(System.Drawing.Color.FromArgb(14, 165, 233)))
                    using (Pen flangeP = new Pen(System.Drawing.Color.FromArgb(3, 105, 161), 1.2f * scale))
                    {
                        g.FillRectangle(flangeB, 1f * scale, 9f * scale, 3.5f * scale, 14f * scale);
                        g.DrawRectangle(flangeP, 1f * scale, 9f * scale, 3.5f * scale, 14f * scale);

                        g.FillRectangle(flangeB, 27.5f * scale, 9f * scale, 3.5f * scale, 14f * scale);
                        g.DrawRectangle(flangeP, 27.5f * scale, 9f * scale, 3.5f * scale, 14f * scale);
                    }

                    // 3. Vạch thước đo mét dài (m) phía dưới ống
                    using (Pen rulerP = new Pen(System.Drawing.Color.FromArgb(245, 158, 11), 1.8f * scale))
                    {
                        g.DrawLine(rulerP, 5f * scale, 26f * scale, 27f * scale, 26f * scale);
                        g.DrawLine(rulerP, 5f * scale, 23f * scale, 5f * scale, 27f * scale);
                        g.DrawLine(rulerP, 16f * scale, 24f * scale, 16f * scale, 27f * scale);
                        g.DrawLine(rulerP, 27f * scale, 23f * scale, 27f * scale, 27f * scale);
                    }

                    // 4. Giọt nước nhỏ phía trên (Màu xanh cyan tươi)
                    using (GraphicsPath dropPath = new GraphicsPath())
                    {
                        dropPath.AddBezier(
                            new PointF(16f * scale, 1f * scale),
                            new PointF(12f * scale, 6f * scale),
                            new PointF(12f * scale, 9f * scale),
                            new PointF(16f * scale, 9f * scale));
                        dropPath.AddBezier(
                            new PointF(16f * scale, 9f * scale),
                            new PointF(20f * scale, 9f * scale),
                            new PointF(20f * scale, 6f * scale),
                            new PointF(16f * scale, 1f * scale));
                        dropPath.CloseFigure();

                        using (SolidBrush dropB = new SolidBrush(System.Drawing.Color.FromArgb(56, 189, 248)))
                        {
                            g.FillPath(dropB, dropPath);
                        }
                    }
                }

                return ConvertToImageSource(bmp);
            }
        }

        /// <summary>
        /// Tạo Icon riêng cho tính năng "Phụ Kiện Ống Nước" (Chuyên bóc Co, Cút, Tê, Van, Côn)
        /// </summary>
        public static ImageSource CreateFittingOnlyIcon(int size)
        {
            using (Bitmap bmp = new Bitmap(size, size))
            {
                using (Graphics g = Graphics.FromImage(bmp))
                {
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                    g.Clear(System.Drawing.Color.Transparent);

                    float scale = size / 32.0f;

                    // 1. Vẽ Co Cút 90 độ (Elbow Fitting) nổi bật phong cách kỹ thuật
                    using (GraphicsPath elbowPath = new GraphicsPath())
                    {
                        elbowPath.AddLine(6f * scale, 26f * scale, 14f * scale, 26f * scale);
                        elbowPath.AddBezier(14f * scale, 26f * scale, 22f * scale, 24f * scale, 26f * scale, 18f * scale, 26f * scale, 10f * scale);
                        elbowPath.AddLine(26f * scale, 10f * scale, 26f * scale, 4f * scale);
                        elbowPath.AddLine(26f * scale, 4f * scale, 18f * scale, 4f * scale);
                        elbowPath.AddBezier(18f * scale, 4f * scale, 18f * scale, 12f * scale, 12f * scale, 18f * scale, 6f * scale, 18f * scale);
                        elbowPath.CloseFigure();

                        using (LinearGradientBrush fitGrad = new LinearGradientBrush(
                            new PointF(6f * scale, 4f * scale), new PointF(26f * scale, 26f * scale),
                            System.Drawing.Color.FromArgb(16, 185, 129), System.Drawing.Color.FromArgb(5, 150, 105)))
                        {
                            g.FillPath(fitGrad, elbowPath);
                        }

                        using (Pen fitPen = new Pen(System.Drawing.Color.FromArgb(6, 78, 59), 1.5f * scale))
                        {
                            g.DrawPath(fitPen, elbowPath);
                        }
                    }

                    // 2. Hai gờ mặt bích nối tại 2 đầu của Co Cút
                    using (SolidBrush flB = new SolidBrush(System.Drawing.Color.FromArgb(52, 211, 153)))
                    using (Pen flP = new Pen(System.Drawing.Color.FromArgb(4, 120, 87), 1.2f * scale))
                    {
                        // Mặt bích dưới
                        g.FillRectangle(flB, 3.5f * scale, 16.5f * scale, 3.5f * scale, 11f * scale);
                        g.DrawRectangle(flP, 3.5f * scale, 16.5f * scale, 3.5f * scale, 11f * scale);

                        // Mặt bích trên
                        g.FillRectangle(flB, 16.5f * scale, 2f * scale, 11f * scale, 3.5f * scale);
                        g.DrawRectangle(flP, 16.5f * scale, 2f * scale, 11f * scale, 3.5f * scale);
                    }

                    // 3. Ký hiệu huy hiệu Số Lượng (Count: #) ở góc dưới bên phải
                    using (SolidBrush badgeB = new SolidBrush(System.Drawing.Color.FromArgb(249, 115, 22)))
                    using (Pen badgeP = new Pen(System.Drawing.Color.White, 1.2f * scale))
                    {
                        g.FillEllipse(badgeB, 17f * scale, 17f * scale, 13f * scale, 13f * scale);
                        g.DrawEllipse(badgeP, 17f * scale, 17f * scale, 13f * scale, 13f * scale);
                    }

                    // Chữ ký hiệu "#" hoặc "PK" bên trong huy hiệu
                    using (Font font = new Font("Segoe UI", 7.0f * scale, FontStyle.Bold))
                    using (SolidBrush textB = new SolidBrush(System.Drawing.Color.White))
                    {
                        g.DrawString("#", font, textB, 19.5f * scale, 16.5f * scale);
                    }
                }

                return ConvertToImageSource(bmp);
            }
        }

                public static ImageSource CreateAutoCropIcon(int size)
        {
            using (Bitmap bmp = new Bitmap(size, size))
            {
                using (Graphics g = Graphics.FromImage(bmp))
                {
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                    g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
                    g.Clear(System.Drawing.Color.Transparent);

                    float scale = size / 32.0f;

                    // 1. Khung viền Crop Box nét đứt màu đỏ / trắng ôm sát
                    using (Pen cropPen = new Pen(System.Drawing.Color.FromArgb(239, 68, 68), 1.8f * scale))
                    {
                        cropPen.DashStyle = DashStyle.Dash;
                        g.DrawRectangle(cropPen, 3f * scale, 3f * scale, 26f * scale, 26f * scale);
                    }

                    // 4 góc tay nắm Crop Box Handles
                    using (SolidBrush handleBrush = new SolidBrush(System.Drawing.Color.FromArgb(239, 68, 68)))
                    {
                        g.FillRectangle(handleBrush, 2f * scale, 2f * scale, 3f * scale, 3f * scale);
                        g.FillRectangle(handleBrush, 27f * scale, 2f * scale, 3f * scale, 3f * scale);
                        g.FillRectangle(handleBrush, 2f * scale, 27f * scale, 3f * scale, 3f * scale);
                        g.FillRectangle(handleBrush, 27f * scale, 27f * scale, 3f * scale, 3f * scale);
                    }

                    // 2. Hệ thống ống mô hình 3D bên trong (Màu tím Plumbing)
                    using (Pen pipePen = new Pen(System.Drawing.Color.FromArgb(192, 132, 252), 2.5f * scale))
                    {
                        pipePen.StartCap = LineCap.Round;
                        pipePen.EndCap = LineCap.Round;
                        // Trục đứng
                        g.DrawLine(pipePen, 16f * scale, 8f * scale, 16f * scale, 24f * scale);
                        // Nhánh rẽ ngang
                        g.DrawLine(pipePen, 16f * scale, 14f * scale, 23f * scale, 10f * scale);
                        g.DrawLine(pipePen, 23f * scale, 10f * scale, 23f * scale, 6f * scale);
                        // Nhánh rẽ trái
                        g.DrawLine(pipePen, 16f * scale, 18f * scale, 8f * scale, 14f * scale);
                    }

                    // Ký hiệu phụ kiện cút / tê tròn nhỏ
                    using (SolidBrush fitBrush = new SolidBrush(System.Drawing.Color.FromArgb(244, 114, 182)))
                    {
                        g.FillEllipse(fitBrush, 14.5f * scale, 12.5f * scale, 3f * scale, 3f * scale);
                        g.FillEllipse(fitBrush, 14.5f * scale, 16.5f * scale, 3f * scale, 3f * scale);
                        g.FillEllipse(fitBrush, 21.5f * scale, 8.5f * scale, 3f * scale, 3f * scale);
                    }

                    // 3. Huy hiệu tia sét Auto ⚡ ở góc phải
                    using (SolidBrush flashBg = new SolidBrush(System.Drawing.Color.FromArgb(245, 158, 11)))
                    using (Pen flashBorder = new Pen(System.Drawing.Color.White, 1.0f * scale))
                    {
                        g.FillEllipse(flashBg, 18f * scale, 18f * scale, 12f * scale, 12f * scale);
                        g.DrawEllipse(flashBorder, 18f * scale, 18f * scale, 12f * scale, 12f * scale);
                    }
                    // Vẽ tia sét ⚡ bằng Vector Polygon chuẩn nét
                    using (SolidBrush flashB = new SolidBrush(System.Drawing.Color.White))
                    {
                        PointF[] bolt = new PointF[] {
                            new PointF(24.5f * scale, 19f * scale),
                            new PointF(21.5f * scale, 24f * scale),
                            new PointF(23.5f * scale, 24f * scale),
                            new PointF(22f * scale, 29f * scale),
                            new PointF(26.5f * scale, 23.5f * scale),
                            new PointF(24.5f * scale, 23.5f * scale)
                        };
                        g.FillPolygon(flashB, bolt);
                    }
                }

                return ConvertToImageSource(bmp);
            }
        }

        /// <summary>
        /// Tạo icon vector cho công cụ Dựng Ống Gió Từ CAD Link
        /// </summary>
        public static ImageSource CreateCadToDuctIcon(int size)
        {
            using (Bitmap bmp = new Bitmap(size, size))
            {
                using (Graphics g = Graphics.FromImage(bmp))
                {
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                    g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
                    g.Clear(Color.Transparent);

                    float s = size / 32.0f;

                    // 1. Than ong CAD (#38bdf8, vien #0284c7)
                    using (SolidBrush ductBrush = new SolidBrush(Color.FromArgb(56, 189, 248)))
                    using (Pen ductBorder = new Pen(Color.FromArgb(2, 132, 199), 1.5f * s))
                    {
                        g.FillRectangle(ductBrush, 3f * s, 10f * s, 16f * s, 12f * s);
                        g.DrawRectangle(ductBorder, 3f * s, 10f * s, 16f * s, 12f * s);
                    }

                    // Duong tim net dut trang
                    using (Pen centerPen = new Pen(Color.White, 1.0f * s))
                    {
                        centerPen.DashPattern = new float[] { 2f, 2f };
                        g.DrawLine(centerPen, 3f * s, 16f * s, 19f * s, 16f * s);
                    }

                    // Chu CAD mau den toi (#0f172a)
                    using (Font font = new Font("Segoe UI", (size >= 32) ? 5.0f * s : 4.0f * s, System.Drawing.FontStyle.Bold))
                    using (SolidBrush textBrush = new SolidBrush(Color.FromArgb(15, 23, 42)))
                    using (StringFormat sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                    {
                        g.DrawString("CAD", font, textBrush, new RectangleF(3f * s, 10f * s, 16f * s, 12f * s), sf);
                    }

                    // 2. Cut cam re nhanh (#f59e0b, vien #d97706)
                    PointF[] tapPts = new PointF[] {
                        new PointF(19f * s, 13f * s),
                        new PointF(25f * s, 7f * s),
                        new PointF(29f * s, 7f * s),
                        new PointF(29f * s, 11f * s),
                        new PointF(23f * s, 17f * s)
                    };
                    using (SolidBrush tapBrush = new SolidBrush(Color.FromArgb(245, 158, 11)))
                    using (Pen tapBorder = new Pen(Color.FromArgb(217, 119, 6), 1.0f * s))
                    {
                        g.FillPolygon(tapBrush, tapPts);
                        g.DrawPolygon(tapBorder, tapPts);
                    }

                    // 3. Ong re nhanh xanh la (#10b981, vien #059669)
                    using (SolidBrush brBrush = new SolidBrush(Color.FromArgb(16, 185, 129)))
                    using (Pen brBorder = new Pen(Color.FromArgb(5, 150, 105), 1.0f * s))
                    {
                        g.FillRectangle(brBrush, 11f * s, 22f * s, 6f * s, 7f * s);
                        g.DrawRectangle(brBorder, 11f * s, 22f * s, 6f * s, 7f * s);
                    }

                    // 4. Dau nhan X mau xanh sky (#38bdf8)
                    using (Pen xPen = new Pen(Color.FromArgb(56, 189, 248), 1.5f * s))
                    {
                        xPen.StartCap = LineCap.Round;
                        xPen.EndCap = LineCap.Round;
                        g.DrawLine(xPen, 23f * s, 23f * s, 29f * s, 27f * s);
                        g.DrawLine(xPen, 29f * s, 23f * s, 23f * s, 27f * s);
                    }
                }

                return ConvertToImageSource(bmp);
            }
        }

        /// <summary>
        /// Tạo icon Ribbon cho lệnh Xuất CAD & Quy Đổi Layer MEP (cmdHDToolExportCad)
        /// </summary>
        public static ImageSource CreateExportCadIcon(int size)
        {
            using (Bitmap bmp = new Bitmap(size, size))
            {
                using (Graphics g = Graphics.FromImage(bmp))
                {
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                    g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
                    g.Clear(Color.Transparent);

                    float s = size / 32.0f;

                    // 1. Man hinh CAD nen den xanh (#0f172a, vien #0284c7)
                    using (GraphicsPath screenPath = new GraphicsPath())
                    {
                        float rad = 3f * s;
                        screenPath.AddArc(3f * s, 3f * s, rad * 2, rad * 2, 180, 90);
                        screenPath.AddArc(29f * s - rad * 2, 3f * s, rad * 2, rad * 2, 270, 90);
                        screenPath.AddArc(29f * s - rad * 2, 29f * s - rad * 2, rad * 2, rad * 2, 0, 90);
                        screenPath.AddArc(3f * s, 29f * s - rad * 2, rad * 2, rad * 2, 90, 90);
                        screenPath.CloseFigure();

                        using (SolidBrush bgBrush = new SolidBrush(Color.FromArgb(15, 23, 42)))
                        using (Pen borderPen = new Pen(Color.FromArgb(2, 132, 199), 1.5f * s))
                        {
                            g.FillPath(bgBrush, screenPath);
                            g.DrawPath(borderPen, screenPath);
                        }
                    }

                    // 2. Duong ngan cach tieu de
                    using (Pen linePen = new Pen(Color.FromArgb(2, 132, 199), 1.0f * s))
                    {
                        g.DrawLine(linePen, 3f * s, 10f * s, 29f * s, 10f * s);
                    }

                    // Chu CAD / DWG (#38bdf8)
                    using (Font font = new Font("Segoe UI", (size >= 32) ? 4.8f * s : 3.8f * s, System.Drawing.FontStyle.Bold))
                    using (SolidBrush textBrush = new SolidBrush(Color.FromArgb(56, 189, 248)))
                    using (StringFormat sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                    {
                        g.DrawString("CAD / DWG", font, textBrush, new RectangleF(3f * s, 3f * s, 26f * s, 7.5f * s), sf);
                    }

                    // 3. Ba vach Layer mau
                    using (SolidBrush cyanBrush = new SolidBrush(Color.FromArgb(6, 182, 212)))
                    using (SolidBrush pinkBrush = new SolidBrush(Color.FromArgb(244, 63, 94)))
                    using (SolidBrush greenBrush = new SolidBrush(Color.FromArgb(16, 185, 129)))
                    {
                        g.FillRectangle(cyanBrush, 6f * s, 13.5f * s, 8f * s, 2.2f * s);
                        g.FillRectangle(pinkBrush, 6f * s, 17.5f * s, 8f * s, 2.2f * s);
                        g.FillRectangle(greenBrush, 6f * s, 21.5f * s, 8f * s, 2.2f * s);
                    }

                    // 4. Nut tam giac cam va cham tron xanh
                    PointF[] playPts = new PointF[] {
                        new PointF(18f * s, 16f * s),
                        new PointF(25f * s, 20f * s),
                        new PointF(18f * s, 24f * s)
                    };
                    using (SolidBrush playBrush = new SolidBrush(Color.FromArgb(245, 158, 11)))
                    using (Pen playBorder = new Pen(Color.FromArgb(217, 119, 6), 0.8f * s))
                    {
                        g.FillPolygon(playBrush, playPts);
                        g.DrawPolygon(playBorder, playPts);
                    }

                    using (SolidBrush dotBrush = new SolidBrush(Color.FromArgb(56, 189, 248)))
                    {
                        g.FillEllipse(dotBrush, 23.5f * s, 18.5f * s, 3f * s, 3f * s);
                    }
                }

                return ConvertToImageSource(bmp);
            }
        }

        /// <summary>
        /// Tạo icon Ribbon cho lệnh Tổng Hợp Family 3D (cmdHDToolFamilyPalette)
        /// </summary>
        public static ImageSource CreateFamilyPaletteIcon(int size)
        {
            using (Bitmap bmp = new Bitmap(size, size))
            {
                using (Graphics g = Graphics.FromImage(bmp))
                {
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                    g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
                    g.Clear(System.Drawing.Color.Transparent);

                    float s = size / 32.0f;

                    // 1. Khối hộp 3D Isometric đại diện cho Family
                    PointF c = new PointF(16f * s, 14f * s);
                    float r = 11f * s;

                    PointF top = new PointF(c.X, c.Y - r * 0.866f);
                    PointF topR = new PointF(c.X + r, c.Y - r * 0.433f);
                    PointF botR = new PointF(c.X + r, c.Y + r * 0.5f);
                    PointF bot = new PointF(c.X, c.Y + r * 0.933f);
                    PointF botL = new PointF(c.X - r, c.Y + r * 0.5f);
                    PointF topL = new PointF(c.X - r, c.Y - r * 0.433f);

                    // Mặt trên (Top Face) - Xanh Cyan sáng
                    PointF[] faceTop = new PointF[] { c, topR, top, topL };
                    using (SolidBrush bTop = new SolidBrush(System.Drawing.Color.FromArgb(56, 189, 248)))
                    {
                        g.FillPolygon(bTop, faceTop);
                    }

                    // Mặt phải (Right Face) - Xanh Navy đậm
                    PointF[] faceRight = new PointF[] { c, topR, botR, bot };
                    using (SolidBrush bRight = new SolidBrush(System.Drawing.Color.FromArgb(2, 132, 199)))
                    {
                        g.FillPolygon(bRight, faceRight);
                    }

                    // Mặt trái (Left Face) - Xanh Emerald / Teal
                    PointF[] faceLeft = new PointF[] { c, topL, botL, bot };
                    using (SolidBrush bLeft = new SolidBrush(System.Drawing.Color.FromArgb(14, 165, 233)))
                    {
                        g.FillPolygon(bLeft, faceLeft);
                    }

                    // Viền sắc nét khối 3D
                    using (Pen pBox = new Pen(System.Drawing.Color.FromArgb(241, 245, 249), 1.0f * s))
                    {
                        g.DrawPolygon(pBox, faceTop);
                        g.DrawPolygon(pBox, faceRight);
                        g.DrawPolygon(pBox, faceLeft);
                    }

                    // 2. Huy hiệu Palette / Thư viện hình tròn màu cam Amber ở góc dưới phải
                    using (SolidBrush bBadge = new SolidBrush(System.Drawing.Color.FromArgb(245, 158, 11)))
                    using (Pen pBadge = new Pen(System.Drawing.Color.White, 1.2f * s))
                    {
                        g.FillEllipse(bBadge, 17f * s, 17f * s, 13f * s, 13f * s);
                        g.DrawEllipse(pBadge, 17f * s, 17f * s, 13f * s, 13f * s);

                        // Dấu cộng "+" trắng ở trung tâm biểu tượng thêm/chọn family
                        using (Pen pPlus = new Pen(System.Drawing.Color.White, 1.8f * s))
                        {
                            pPlus.StartCap = LineCap.Round;
                            pPlus.EndCap = LineCap.Round;
                            g.DrawLine(pPlus, 23.5f * s, 20f * s, 23.5f * s, 27f * s);
                            g.DrawLine(pPlus, 20f * s, 23.5f * s, 27f * s, 23.5f * s);
                        }
                    }
                }

                return ConvertToImageSource(bmp);
            }
        }

        private static ImageSource ConvertToImageSource(Bitmap bitmap)
        {
            using (MemoryStream memory = new MemoryStream())
            {
                bitmap.Save(memory, System.Drawing.Imaging.ImageFormat.Png);
                memory.Position = 0;

                BitmapImage bitmapImage = new BitmapImage();
                bitmapImage.BeginInit();
                bitmapImage.StreamSource = memory;
                bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
                bitmapImage.EndInit();
                bitmapImage.Freeze();

                return bitmapImage;
            }
        }
    }
}
