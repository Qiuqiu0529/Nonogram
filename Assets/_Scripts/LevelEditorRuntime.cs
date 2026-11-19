using System;
using System.Collections.Generic;
using System.IO;
using Nonogram;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>Runtime level editor UI used by the LevelEditor scene.</summary>
public partial class LevelEditorRuntime : MonoBehaviour
{
    static readonly string[] Colors = { "#76564C", "#F5AFA9", "#FFD36E", "#91B978", "#A6A5DF", "#FFD36E" };
    static readonly string[] Shapes = { "●", "▲", "◆", "■", "★", "♥" };
    static readonly string[] ShapeIds = { "circle", "triangle", "diamond", "square", "star", "heart" };
    static Sprite[] shapeSprites;
    NonogramLevelData level;
    bool checkingUniqueSolution;
    bool[] ambiguousRows, ambiguousColumns;
    string savedLevelFilename;
    string savedLevelSnapshot;
    GameObject cellPrefab;
    [SerializeField] GameObject levelBackgroundPrefab;
    [SerializeField] Font clueFont;
    RectTransform boardViewport;
    NonogramBoardLayout boardLayout;
    RectTransform root, brushRoot, colorRoot, shapeRoot;
    Text widthLabel, heightLabel, statusLabel, modeButtonLabel, displayButtonLabel;
    Slider widthSlider, heightSlider;
    int activeBrush;
    LevelDifficulty difficulty;
    Text difficultyLabel;
    bool markMode;
    bool patternView;
    Sprite defaultInsideSprite;
    int width = 10, height = 10;
    [SerializeField, Min(1), InspectorName("数字提示最大字号")] int clueFontSize = 14;
    [SerializeField, Min(1), InspectorName("数字提示最小字号")] int clueFontMinSize = 12;
    [SerializeField] FontStyle clueFontStyle = FontStyle.Normal;
    [SerializeField, Range(.1f, 1f)] float boardWidthRatio = .9f;
    [SerializeField, Min(0f)] float backgroundTopPadding = 18f;
    [SerializeField, Min(0f)] float backgroundLeftPadding = 18f;
    [SerializeField, Min(0f)] float backgroundRightPadding = 36f;
    [SerializeField, Min(0f)] float backgroundBottomPadding = 36f;
    bool[,] changedDuringDrag;
    Font uiFont;

    void Start()
    {
        try
        {
            cellPrefab = Resources.Load<GameObject>("Grid");
            if (!cellPrefab) throw new InvalidOperationException("找不到 Resources/Grid.prefab");
            if (!levelBackgroundPrefab) levelBackgroundPrefab = Resources.Load<GameObject>("LevelBackground");
            if(!levelBackgroundPrefab)throw new InvalidOperationException("找不到 Resources/LevelBackground.prefab");
            var prefabInside=cellPrefab.transform.Find("ImageInside");
            if(prefabInside){var prefabImage=prefabInside.GetComponent<Image>();if(prefabImage)defaultInsideSprite=prefabImage.sprite;}
            uiFont = Resources.Load<Font>("SourceHanSansCN-Medium");
            if (!uiFont) throw new InvalidOperationException("找不到 SourceHanSansCN-Medium 字体资源");
            if (shapeSprites == null) shapeSprites = BuildShapeSprites();
            if (!FindObjectOfType<EventSystem>()) new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            BuildUI();
            UpdateSizeLabels();
        }
        catch (Exception e) { Debug.LogException(e); }
    }

    void BuildUI()
    {
        var canvasGO = new GameObject("Level Editor Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var canvas = canvasGO.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasGO.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(900, 1100); scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
        var bg = Panel(canvasGO.transform, "Background", new Color32(250,246,240,255)); Stretch(bg.GetComponent<RectTransform>()); root = bg.GetComponent<RectTransform>();
        var controls = CreateRect("Editor Controls", root, Vector2.zero, new Vector2(850f, 540f));
        controls.anchorMin = controls.anchorMax = new Vector2(.5f, 1f);
        controls.pivot = new Vector2(.5f, 1f);
        Label(controls, "🐾  猫咪数织关卡编辑器", 34, new Vector2(0,-52), new Vector2(820,70), TextAnchor.MiddleCenter);
        Label(controls, "设置棋盘大小，选择画笔后点击格子绘制", 19, new Vector2(0,-102), new Vector2(820,40), TextAnchor.MiddleCenter);
        widthLabel = Label(controls,"宽度：10",20,new Vector2(-195,-158),new Vector2(240,38),TextAnchor.MiddleCenter);
        heightLabel = Label(controls,"高度：10",20,new Vector2(195,-158),new Vector2(240,38),TextAnchor.MiddleCenter);
        widthSlider = MakeSlider(controls,new Vector2(-195,-202)); widthSlider.onValueChanged.AddListener(v=>{width=Mathf.RoundToInt(v);UpdateSizeLabels();ShowPendingSize();});
        heightSlider = MakeSlider(controls,new Vector2(195,-202)); heightSlider.onValueChanged.AddListener(v=>{height=Mathf.RoundToInt(v);UpdateSizeLabels();ShowPendingSize();});
        Button(controls,"新建关卡",new Vector2(-250,-260),new Vector2(220,58),CreateLevel);
        modeButtonLabel=Button(controls,"填充 / 标记",new Vector2(0,-260),new Vector2(220,58),ToggleMode);
        Button(controls,"保存关卡",new Vector2(250,-260),new Vector2(220,58),SaveLevel);
        statusLabel=Label(controls,"先调整尺寸并点击「新建关卡」",16,new Vector2(-150,-460),new Vector2(500,45),TextAnchor.MiddleCenter);
        displayButtonLabel=Button(controls,"查看：颜色",new Vector2(290,-460),new Vector2(190,38),ToggleDisplayMode);
        statusLabel.horizontalOverflow = HorizontalWrapMode.Wrap;
        statusLabel.verticalOverflow = VerticalWrapMode.Truncate;
        brushRoot = CreateRect("Brushes", controls, new Vector2(0, -324), new Vector2(760, 58));
        colorRoot = CreateBrushOptions(controls, "画笔颜色", -210f);
        shapeRoot = CreateBrushOptions(controls, "画笔形状", 210f);
        difficultyLabel = Button(controls, "难度：普通", new Vector2(0, -510), new Vector2(220, 42), CycleDifficulty);
        Button(controls, "自选颜色", new Vector2(-245, -510), new Vector2(200, 42), OpenBrushColorPicker);
        Button(controls, "检查唯一解", new Vector2(245, -510), new Vector2(200, 42), CheckUniqueSolution);
        Button(controls, "试玩当前关卡", new Vector2(0, 180), new Vector2(240, 50), StartPlaytest);
        Button(controls, "加载关卡", new Vector2(-290, 180), new Vector2(210, 50), OpenLoadLevelList);
        Button(controls, "调整当前尺寸", new Vector2(290, 180), new Vector2(210, 50), ResizeCurrentLevel);
        boardViewport = CreateRect("Board Viewport", root, Vector2.zero, Vector2.zero);
        Stretch(boardViewport);
        boardViewport.offsetMin = new Vector2(0f, 20f);
        boardViewport.offsetMax = new Vector2(0f, -controls.sizeDelta.y - 18f);
    }

    void CycleDifficulty()
    {
        difficulty = (LevelDifficulty)(((int)difficulty + 1) % 3);
        difficultyLabel.text = difficulty == LevelDifficulty.Difficult ? "难度：困难"
            : difficulty == LevelDifficulty.Super ? "难度：超难" : "难度：普通";
        if (level != null) level.difficulty = difficulty;
    }

    RectTransform CreateBrushOptions(RectTransform controls, string title, float x)
    {
        var group = CreateRect(title, controls, new Vector2(x, -390f), new Vector2(390f, 78f));
        Label(group, title, 15, new Vector2(0, 25f), new Vector2(390f, 24f), TextAnchor.MiddleCenter);
        return CreateRect("Options", group, new Vector2(0, -13f), new Vector2(390f, 48f));
    }

    Slider MakeSlider(Transform parent, Vector2 pos, int min = 5, int max = 20, int initial = 10)
    {
        var go=Panel(parent,"Size Slider",new Color32(232,220,217,255)); var rect=go.GetComponent<RectTransform>(); Place(rect,pos,new Vector2(340,24));
        var fillArea=CreateRect("Fill Area",rect,new Vector2(0,0),new Vector2(320,12)); var fill=Panel(fillArea,"Fill",new Color32(231,137,168,255)); Stretch(fill.GetComponent<RectTransform>());
        var handle=Panel(rect,"Handle",Color.white); Place(handle.GetComponent<RectTransform>(),new Vector2(0,0),new Vector2(32,32));
        var slider=go.AddComponent<Slider>(); slider.minValue=min;slider.maxValue=max;slider.wholeNumbers=true;slider.value=initial;slider.direction=Slider.Direction.LeftToRight;slider.fillRect=fill.GetComponent<RectTransform>();slider.handleRect=handle.GetComponent<RectTransform>();slider.targetGraphic=handle.GetComponent<Image>(); return slider;
    }

    void OpenBrushColorPicker()
    {
        if (level == null)
        {
            statusLabel.text = "请先新建关卡并选择画笔";
            return;
        }

        var brush = level.brushes[activeBrush];
        Color32 draft = ColorUtility.TryParseHtmlString(brush.colorHex, out var initial)
            ? (Color32)initial : new Color32(255, 255, 255, 255);
        draft.a = 255;

        var overlay = Panel(root, "Brush Color Picker", new Color(0f, 0f, 0f, .48f));
        Stretch((RectTransform)overlay.transform);
        var dialog = Panel(overlay.transform, "Color Dialog", new Color32(255, 249, 245, 255));
        Place((RectTransform)dialog.transform, Vector2.zero, new Vector2(560, 440));
        Label(dialog.transform, "自选画笔颜色", 28, new Vector2(0, 183),
            new Vector2(460, 42), TextAnchor.MiddleCenter);

        var preview = Panel(dialog.transform, "Color Preview", draft).GetComponent<Image>();
        Place(preview.rectTransform, new Vector2(-46, 113), new Vector2(188, 54));
        var hexLabel = Label(dialog.transform, "", 20, new Vector2(137, 113),
            new Vector2(160, 54), TextAnchor.MiddleCenter);

        void RefreshPreview()
        {
            preview.color = draft;
            hexLabel.text = "#" + ColorUtility.ToHtmlStringRGB(draft);
        }

        void AddChannel(string name, float y, byte initialValue, Action<byte> setChannel)
        {
            Label(dialog.transform, name, 20, new Vector2(-210, y),
                new Vector2(48, 40), TextAnchor.MiddleCenter);
            var slider = MakeSlider(dialog.transform, new Vector2(-12, y), 0, 255, initialValue);
            var valueLabel = Label(dialog.transform, initialValue.ToString(), 20, new Vector2(197, y),
                new Vector2(70, 40), TextAnchor.MiddleCenter);
            slider.onValueChanged.AddListener(value =>
            {
                byte channel = (byte)value;
                setChannel(channel);
                valueLabel.text = channel.ToString();
                RefreshPreview();
            });
        }

        void ClosePicker()
        {
            overlay.SetActive(false);
            Destroy(overlay);
        }

        AddChannel("R", 45f, draft.r, value => draft.r = value);
        AddChannel("G", -14f, draft.g, value => draft.g = value);
        AddChannel("B", -73f, draft.b, value => draft.b = value);
        RefreshPreview();
        Button(dialog.transform, "取消", new Vector2(-112, -163), new Vector2(190, 52), ClosePicker);
        Button(dialog.transform, "确定", new Vector2(112, -163), new Vector2(190, 52), () =>
        {
            brush.colorHex = hexLabel.text;
            ClosePicker();
            BuildBrushes();
            RenderBoard();
        });
    }

    void CreateLevel()
    {
        ClearAmbiguity();
        savedLevelFilename = null;
        savedLevelSnapshot = null;
        patternView = false;
        displayButtonLabel.text = "查看：颜色";
        level=new NonogramLevelData(width,height){id=Guid.NewGuid().ToString("N"),title="猫咪新关卡",difficulty=difficulty,clueDisplay=FillKind.Color,
            brushes=new[]{new NonogramBrush("brush-1","粉色圆点",Colors[0],Shapes[0])}};
        activeBrush=0;markMode=false;modeButtonLabel.text="当前：填充模式";BuildBrushes();RenderBoard();statusLabel.text="棋盘已创建："+width+" × "+height+"。选择画笔并绘制。";
    }

    void BuildBrushes()
    {
        Clear(brushRoot);
        if(level==null)return;
        for(int i=0;i<level.brushes.Length;i++)
        {
            int index=i;var brush=level.brushes[i];var go=Panel(brushRoot,"Brush",Color.white);Place(go.GetComponent<RectTransform>(),new Vector2(-240+i*112,0),new Vector2(96,52));
            var image=go.GetComponent<Image>();image.sprite=GetShapeSprite(brush.shape);Color c;if(ColorUtility.TryParseHtmlString(brush.colorHex,out c))image.color=c;
            var button=go.AddComponent<Button>();button.targetGraphic=image;button.onClick.AddListener(()=>{activeBrush=index;statusLabel.text="已选择画笔："+level.brushes[index].name;BuildBrushOptions();});
        }
        if (level.brushes.Length < Colors.Length)
            Button(brushRoot,"＋画笔",new Vector2(-240+level.brushes.Length*112,0),new Vector2(96,52),AddBrush);
        BuildBrushOptions();
    }

    void BuildBrushOptions()
    {
        Clear(colorRoot);Clear(shapeRoot);if(level==null||level.brushes.Length==0)return;
        var brush=level.brushes[Mathf.Clamp(activeBrush,0,level.brushes.Length-1)];
        for(int i=0;i<Colors.Length;i++){int choice=i;var go=Panel(colorRoot,"ColorOption",Color.white);Place(go.GetComponent<RectTransform>(),new Vector2(-150+i*60,0),new Vector2(42,42));Color c;ColorUtility.TryParseHtmlString(Colors[i],out c);go.GetComponent<Image>().color=c;var b=go.AddComponent<Button>();b.onClick.AddListener(()=>{brush.colorHex=Colors[choice];BuildBrushes();RenderBoard();});}
        for(int i=0;i<ShapeIds.Length;i++){int choice=i;var go=Panel(shapeRoot,"ShapeOption",Color.white);Place(go.GetComponent<RectTransform>(),new Vector2(-150+i*60,0),new Vector2(42,42));var image=go.GetComponent<Image>();image.sprite=shapeSprites[i];var b=go.AddComponent<Button>();b.targetGraphic=image;b.onClick.AddListener(()=>{brush.shape=ShapeIds[choice];BuildBrushes();RenderBoard();});}
    }

    internal static Sprite GetShapeSprite(string shape)
    {
        if (shapeSprites == null) shapeSprites = BuildShapeSprites();
        int index = Array.IndexOf(ShapeIds, shape);
        if (index < 0) index = Array.IndexOf(Shapes, shape);
        return shapeSprites[Mathf.Max(0, index)];
    }
    static Sprite[] BuildShapeSprites()
    {
        var result=new Sprite[ShapeIds.Length];const int n=48;
        for(int s=0;s<result.Length;s++)
        {
            var tex=new Texture2D(n,n,TextureFormat.RGBA32,false);tex.filterMode=FilterMode.Point;var pixels=new Color[n*n];
            for(int y=0;y<n;y++)for(int x=0;x<n;x++){float px=(x+0.5f-n*.5f)/(n*.42f),py=(y+0.5f-n*.5f)/(n*.42f);bool inside=false;
                switch(s){case 0:inside=px*px+py*py<=1;break;case 1:inside=py>=-0.9f&&py<=0.85f&&Mathf.Abs(px)<=((0.85f-py)*.58f);break;case 2:inside=Mathf.Abs(px)+Mathf.Abs(py)<=1;break;case 3:inside=Mathf.Abs(px)<=.82f&&Mathf.Abs(py)<=.82f;break;case 4:float a=Mathf.Atan2(py,px);float r=.58f+.38f*Mathf.Cos(5*a);inside=Mathf.Sqrt(px*px+py*py)<=r;break;case 5:float heart=(px*px+py*py-1);inside=heart*heart*heart-px*px*py*py*py<=0;break;}
                pixels[y*n+x]=inside?Color.white:Color.clear;}
            tex.SetPixels(pixels);tex.Apply();result[s]=Sprite.Create(tex,new Rect(0,0,n,n),new Vector2(.5f,.5f),n);
        }return result;
    }

    void AddBrush()
    {
        if(level==null)return;
        int i=level.brushes.Length;if(i>=Colors.Length){statusLabel.text="最多支持 "+Colors.Length+" 种画笔";return;}
        Array.Resize(ref level.brushes,i+1);level.brushes[i]=new NonogramBrush("brush-"+(i+1),"画笔 "+(i+1),Colors[i],Shapes[i]);activeBrush=i;BuildBrushes();
    }
    void ToggleMode(){markMode=!markMode;if(modeButtonLabel)modeButtonLabel.text=markMode?"当前：标记模式":"当前：填充模式";}
    void ToggleDisplayMode(){patternView=!patternView;if(displayButtonLabel)displayButtonLabel.text=patternView?"查看：图案":"查看：颜色";if(level!=null){level.clueDisplay=patternView?FillKind.Shape:FillKind.Color;RenderBoard();}}

    void RenderBoard()
    {
        if (level == null) return;
        if (boardLayout)
        {
            boardLayout.gameObject.SetActive(false);
            Destroy(boardLayout.gameObject);
        }
        var padding = new Vector4(backgroundTopPadding, backgroundLeftPadding,
            backgroundRightPadding, backgroundBottomPadding);
        boardLayout = NonogramBoardLayout.Create(boardViewport, levelBackgroundPrefab, cellPrefab, clueFont ? clueFont : uiFont,
            level, patternView, (cell, x, y) =>
            {
                ApplyCell(cell.transform, level.GetCell(x, y));
                cell.AddComponent<CellClick>().Init(() => EditCell(x, y));
            }, padding, clueFontSize, clueFontStyle, boardWidthRatio,
            minFontSize: clueFontMinSize);
        boardLayout.SetSeparatorOpacity(1f);
        if (ambiguousRows != null || ambiguousColumns != null)
            boardLayout.SetAmbiguousLines(ambiguousRows, ambiguousColumns);
    }

    void EditCell(int x,int y)
    {
        if(level==null)return;var cell=level.GetCell(x,y);
        ClearAmbiguity();
        if(markMode)cell.state=cell.state==CellState.Marked?CellState.Blank:CellState.Marked;
        else if(cell.state==CellState.Filled&&cell.brushId==activeBrush)cell.state=CellState.Blank;
        else{cell.state=CellState.Filled;cell.brushId=activeBrush;}
        level.SetCell(x,y,cell);RenderBoard();
    }

    void ApplyCell(Transform cellRoot,NonogramCell cell)
    {
        var inside=cellRoot.Find("ImageInside");var tag=cellRoot.Find("ImageTag");var image=inside?inside.GetComponent<Image>():null;
        if(image)
        {
            image.enabled=true;
            if(cell.state==CellState.Filled && cell.brushId>=0 && cell.brushId<level.brushes.Length)
            {
                var brush=level.brushes[cell.brushId];
                Color color = new Color32(65, 52, 59, 255);
                if (!patternView && ColorUtility.TryParseHtmlString(brush.colorHex, out var brushColor))
                    color = brushColor;
                image.color=color;
                image.sprite=patternView?GetShapeSprite(brush.shape):defaultInsideSprite;
            }
            else
            {
                image.color=new Color32(255,255,255,255);
                image.sprite=defaultInsideSprite;
            }
        }
        if(tag)tag.gameObject.SetActive(cell.state==CellState.Marked);
    }

    async void CheckUniqueSolution()
    {
        if (level == null)
        {
            statusLabel.text = "请先新建关卡";
            return;
        }
        if (checkingUniqueSolution) return;
        ClearAmbiguity();
        checkingUniqueSolution = true;
        try
        {
            var checkedLevel = level;
            string snapshot = NonogramLevelJson.Serialize(checkedLevel, false);
            int checkedWidth = checkedLevel.width;
            int checkedHeight = checkedLevel.height;
            // RebuildClues replaces the clue object, so later drawing cannot change this snapshot.
            var checkedClues = checkedLevel.clues;
            var authored = new int[checkedLevel.cells.Length];
            for (int i = 0; i < authored.Length; i++)
                authored[i] = checkedLevel.cells[i].state == CellState.Filled
                    ? checkedLevel.cells[i].brushId + 1 : 0;
            statusLabel.text = "正在检查唯一解…";
            var report = await System.Threading.Tasks.Task.Run(() =>
                NonogramUniqueness.CheckDetailed(checkedWidth, checkedHeight, checkedClues, authored));
            if (!this || !statusLabel) return;
            if (level != checkedLevel || snapshot != NonogramLevelJson.Serialize(level, false))
            {
                statusLabel.text = "关卡已修改，请重新检查";
                return;
            }
            switch (report.result)
            {
                case NonogramUniqueness.Result.Unique:
                    statusLabel.text = "检查通过：该关卡有且只有一个解";
                    break;
                case NonogramUniqueness.Result.Multiple:
                    ambiguousRows = report.differingRows;
                    ambiguousColumns = report.differingColumns;
                    if (boardLayout) boardLayout.SetAmbiguousLines(ambiguousRows, ambiguousColumns);
                    statusLabel.text = "存在多个解：橙色标出分歧的" +
                        DescribeAmbiguousLines(ambiguousRows, "行") + "、" +
                        DescribeAmbiguousLines(ambiguousColumns, "列");
                    break;
                case NonogramUniqueness.Result.NoSolution:
                    statusLabel.text = "无解，请检查行列线索";
                    break;
                default:
                    statusLabel.text = "计算量较大，暂未能确认是否唯一";
                    break;
            }
        }
        catch (Exception exception)
        {
            if (this && statusLabel) statusLabel.text = "检查失败：" + exception.Message;
            Debug.LogException(exception);
        }
        finally { checkingUniqueSolution = false; }
    }

    void ClearAmbiguity()
    {
        ambiguousRows = null;
        ambiguousColumns = null;
        if (boardLayout) boardLayout.SetAmbiguousLines(null, null);
    }

    static string DescribeAmbiguousLines(bool[] lines, string suffix)
    {
        if (lines == null) return "未知" + suffix;
        var numbers = new List<string>();
        int total = 0;
        for (int i = 0; i < lines.Length; i++)
        {
            if (!lines[i]) continue;
            total++;
            if (numbers.Count < 5) numbers.Add((i + 1).ToString());
        }
        return total == 0 ? "未知" + suffix :
            "第" + string.Join("、", numbers) + (total > numbers.Count ? "等" : "") + suffix;
    }

    void SaveLevel()
    {
        if (level == null)
        {
            statusLabel.text = "请先新建关卡";
            return;
        }
        level.difficulty = difficulty;
        try
        {
            string folder = NonogramCatalog.DirectoryPath;
            string filename = savedLevelFilename;
            if (filename == null)
            {
                DateTime stamp = DateTime.Now;
                do
                {
                    filename = "Nonogram_" + stamp.ToString("yyyyMMdd_HHmmss_fff",
                        System.Globalization.CultureInfo.InvariantCulture) + ".json";
                    stamp = stamp.AddMilliseconds(1);
                }
                while (File.Exists(Path.Combine(folder, filename)));
            }
            NonogramLevelJson.SaveFile(Path.Combine(folder, filename), level);
            savedLevelFilename = filename;
            savedLevelSnapshot = NonogramLevelJson.Serialize(level, false);
            statusLabel.text = "已保存 " + filename;
        }
        catch (Exception e)
        {
            Debug.LogException(e);
            statusLabel.text = "保存失败：" + e.Message;
        }
    }
    void UpdateSizeLabels(){if(widthLabel)widthLabel.text="宽度："+width;if(heightLabel)heightLabel.text="高度："+height;}

    GameObject Panel(Transform parent,string name,Color color){var go=new GameObject(name,typeof(RectTransform),typeof(Image));go.transform.SetParent(parent,false);go.GetComponent<Image>().color=color;return go;}
    Text Label(Transform parent,string value,int size,Vector2 pos,Vector2 dimensions,TextAnchor anchor){var go=new GameObject("Label",typeof(RectTransform),typeof(Text));go.transform.SetParent(parent,false);var t=go.GetComponent<Text>();t.font=uiFont;t.fontSize=size;t.color=new Color32(65,52,59,255);t.text=value;t.alignment=anchor;t.supportRichText=true;t.horizontalOverflow=HorizontalWrapMode.Overflow;t.verticalOverflow=VerticalWrapMode.Overflow;Place(t.rectTransform,pos,dimensions);return t;}
    Text Label(Transform p,string s,Vector2 pos,Vector2 dim,TextAnchor anchor)=>Label(p,s,16,pos,dim,anchor);
    Text Button(Transform parent,string label,Vector2 pos,Vector2 size,Action action){var go=Panel(parent,"Button",new Color32(250,231,235,255));Place(go.GetComponent<RectTransform>(),pos,size);var b=go.AddComponent<Button>();b.onClick.AddListener(()=>action());return Label(go.transform,label,18,Vector2.zero,size,TextAnchor.MiddleCenter);}
    RectTransform CreateRect(string name,Transform parent,Vector2 pos,Vector2 size){var go=new GameObject(name,typeof(RectTransform));go.transform.SetParent(parent,false);var r=go.GetComponent<RectTransform>();Place(r,pos,size);return r;}
    void Place(RectTransform r,Vector2 pos,Vector2 size){r.anchorMin=r.anchorMax=new Vector2(.5f,.5f);r.anchoredPosition=pos;r.sizeDelta=size;}
    void Stretch(RectTransform r){r.anchorMin=Vector2.zero;r.anchorMax=Vector2.one;r.offsetMin=r.offsetMax=Vector2.zero;}
    void Clear(Transform parent)
    {
        for (int i = parent.childCount - 1; i >= 0; i--)
        {
            var child = parent.GetChild(i).gameObject;
            child.SetActive(false);
            Destroy(child);
        }
    }

    class CellClick:MonoBehaviour,IPointerClickHandler
    {Action callback;public void Init(Action action){callback=action;}public void OnPointerClick(PointerEventData eventData){callback?.Invoke();}}
}
