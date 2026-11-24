using Microsoft.Maui.Graphics;
using NCalc;

namespace DESMOS;

public class GraphDrawable : IDrawable
{
    public float Scale { get; set; } = 50f;
    public float OffsetX { get; set; } = 0f;
    public float OffsetY { get; set; } = 0f;
    public double SelectedMathX { get; set; }
    public double SelectedMathY { get; set; }


    // ХРАНИМ СПИСОК ВСЕХ ФУНКЦИЙ И ПАРАМЕТРОВ
    public IList<GraphFunction> Functions { get; set; } = new List<GraphFunction>();
    public IList<GraphParameter> Parameters { get; set; } = new List<GraphParameter>();

    public void Draw(ICanvas canvas, RectF dirtyRect)
    {
        canvas.FillColor = Colors.White;
        canvas.FillRectangle(dirtyRect);

        float originX = (dirtyRect.Width / 2) + OffsetX;
        float originY = (dirtyRect.Height / 2) + OffsetY;

        float targetPixelStep = 80f;
        float mathStep = targetPixelStep / Scale;
        float niceStep = GetNiceStep(mathStep);

        canvas.StrokeColor = Colors.LightGray;
        canvas.StrokeSize = 1;
        canvas.FontColor = Colors.Black;
        canvas.FontSize = 12;



        // Вертикальные линии (ось X)
        float startX = (float)Math.Floor((-originX / Scale) / niceStep) * niceStep;
        int safety = 0;
        for (double mathX = startX; safety++ < 200; mathX += niceStep)
        {
            float pixelX = originX + (float)(mathX * Scale);
            if (pixelX > dirtyRect.Width) break;

            canvas.DrawLine(pixelX, 0, pixelX, dirtyRect.Height);
            if (Math.Abs(mathX) > 0.0001)
            {
                canvas.DrawString(mathX.ToString("0.###"), pixelX - 15, originY + 5, 30, 20, HorizontalAlignment.Center, VerticalAlignment.Top);
            }
        }

        // Горизонтальные линии (ось Y)
        float bottomMathY = (originY - dirtyRect.Height) / Scale;
        float startY = (float)Math.Floor(bottomMathY / niceStep) * niceStep;
        safety = 0;
        for (double mathY = startY; safety++ < 200; mathY += niceStep)
        {
            float pixelY = originY - (float)(mathY * Scale);
            if (pixelY < 0) break;

            canvas.DrawLine(0, pixelY, dirtyRect.Width, pixelY);
            if (Math.Abs(mathY) > 0.0001)
            {
                canvas.DrawString(mathY.ToString("0.###"), originX - 30, pixelY - 10, 25, 20, HorizontalAlignment.Right, VerticalAlignment.Center);
            }
        }

        // Оси координат
        canvas.StrokeColor = Colors.Black;
        canvas.StrokeSize = 1.5f;
        canvas.DrawLine(0, originY, dirtyRect.Width, originY);
        canvas.DrawLine(originX, 0, originX, dirtyRect.Height);
        canvas.DrawString("0", originX - 15, originY + 5, 30, 20, HorizontalAlignment.Right, VerticalAlignment.Top);

        // Рисуем каждую функцию из списка своим цветом
        if (Functions == null) return;

        foreach (var func in Functions)
        {
            DrawSingleFunction(canvas, dirtyRect, originX, originY, func);
        }

        //if (Functions != null)
        //{
        //    canvas.FillColor = Colors.Red;
        //    double bestSolution = 99999;
        //    double bestY = 0;

        //    foreach (var func in Functions)
        //    {

        //        var expression = new Expression(func.Formula);
        //        double MathX = (SelectedMathX - originX) / Scale;

        //        double MathY = - (SelectedMathY - originY) / Scale;


        //        expression.Parameters["x"] = MathX;

        //        double y = Convert.ToDouble(expression.Evaluate());

        //        if (bestSolution > Math.Abs(y - MathY))
        //        {

        //            bestSolution = Math.Abs(y - MathY);
        //            bestY = y;


        //            System.Diagnostics.Debug.WriteLine(bestSolution);

        //            System.Diagnostics.Debug.WriteLine(bestY);

        //        }

        //    }
            
        //    System.Diagnostics.Debug.WriteLine((float)(bestY * Scale + originY));

        //    canvas.FillCircle((float)SelectedMathX, (float)(originY - bestY * Scale), 5);
        //}
    }

    private void DrawSingleFunction(ICanvas canvas, RectF dirtyRect, float originX, float originY, GraphFunction func)
    {
        if (string.IsNullOrWhiteSpace(func.Formula)) return;

        Expression expression;
        try
        {
            expression = new Expression(func.Formula);

            if (Parameters != null)
            {
                foreach (var param in Parameters)
                    expression.Parameters[param.Name] = param.Value;
            }

            if (expression.HasErrors()) return;
            expression.Parameters["x"] = 0.0;
            expression.Evaluate();
        }
        catch { return; }

        canvas.StrokeColor = func.LineColor;
        canvas.StrokeSize = 2f;
        canvas.StrokeLineJoin = LineJoin.Round;
        canvas.StrokeLineCap = LineCap.Round;

        var path = new PathF();
        bool hasAnyLines = false;
        bool hasPrevPoint = false;
        float prevX = 0f, prevY = 0f;

        float stepX = 0.5f;
        // При отдалении (маленьком Scale) проверяем больше промежуточных точек внутри шага, чтобы не пропустить пик асимптоты
        int subChecks = Scale < 10f ? 16 : (Scale < 30f ? 8 : 2);

        for (float pixelX = 0; pixelX < dirtyRect.Width; pixelX += stepX)
        {
            double mathX = (pixelX - originX) / Scale;
            if (!TryEvaluatePixelY(expression, mathX, originY, Scale, dirtyRect.Height, out float pixelY))
            {
                hasPrevPoint = false;
                continue;
            }

            if (hasPrevPoint)
            {
                // Проверяем, не спряталась ли асимптота между prevX и pixelX
                bool hasAsymptote = (prevY < 0 && pixelY > dirtyRect.Height) ||
                                    (prevY > dirtyRect.Height && pixelY < 0) ||
                                    (Math.Abs(pixelY - prevY) > dirtyRect.Height * 0.7f &&
                                     ((prevY < originY && pixelY > originY) || (prevY > originY && pixelY < originY)));

                if (!hasAsymptote && subChecks > 1)
                {
                    float lastSubY = prevY;
                    for (int s = 1; s < subChecks; s++)
                    {
                        float subPx = prevX + (stepX * s / subChecks);
                        double subMathX = (subPx - originX) / Scale;
                        if (TryEvaluatePixelY(expression, subMathX, originY, Scale, dirtyRect.Height, out float subY))
                        {
                            if ((lastSubY < 0 && subY > dirtyRect.Height) ||
                                (lastSubY > dirtyRect.Height && subY < 0) ||
                                (Math.Abs(subY - lastSubY) > dirtyRect.Height * 0.5f &&
                                 ((lastSubY < originY && subY > originY) || (lastSubY > originY && subY < originY))))
                            {
                                hasAsymptote = true;
                                break;
                            }
                            lastSubY = subY;
                        }
                    }
                }

                if (hasAsymptote)
                {
                    // Дотягиваем левую ветвь до края экрана
                    float edge1 = prevY < originY ? -10f : dirtyRect.Height + 10f;
                    path.MoveTo(prevX, prevY);
                    path.LineTo(pixelX, edge1);

                    // Начинаем правую ветвь от противоположного края
                    float edge2 = pixelY < originY ? -10f : dirtyRect.Height + 10f;
                    path.MoveTo(pixelX, edge2);
                    path.LineTo(pixelX, pixelY);
                }
                else
                {
                    // Обычный гладкий отрезок
                    path.MoveTo(prevX, prevY);
                    path.LineTo(pixelX, pixelY);
                }
                hasAnyLines = true;
            }

            prevX = pixelX;
            prevY = pixelY;
            hasPrevPoint = true;
        }

        if (hasAnyLines)
        {
            canvas.DrawPath(path);
        }
    }


    private bool TryEvaluatePixelY(Expression expression, double mathX, float originY, float scale, float screenHeight, out float pixelY)
    {
        pixelY = 0f;
        try
        {
            expression.Parameters["x"] = mathX;
            float mathY = Convert.ToSingle(expression.Evaluate());
            if (float.IsNaN(mathY) || float.IsInfinity(mathY)) return false;

            pixelY = Math.Clamp(originY - (mathY * scale), -5000f, screenHeight + 5000f);
            return true;
        }
        catch
        {
            return false;
        }
    }


    private float GetNiceStep(float roughStep)
    {
        float exponent = (float)Math.Floor(Math.Log10(roughStep));
        float fraction = roughStep / (float)Math.Pow(10, exponent);

        float niceFraction = fraction <= 1.5f ? 1 : fraction <= 3f ? 2 : fraction <= 7f ? 5 : 10;
        return niceFraction * (float)Math.Pow(10, exponent);
    }
}
