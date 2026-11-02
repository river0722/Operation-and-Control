
import zipfile, io
for k,p in {"B":r"D:\pmac资料\output\5.2_功能模块规划与工艺流程\5.2_功能模块规划与工艺流程.docx",
            "C":r"D:\pmac资料\output\5.3_上位机绘图与CAD文件读取\5.3_上位机绘图与CAD文件读取.docx"}.items():
    with zipfile.ZipFile(p) as z:
        for n in z.namelist():
            if n.startswith("word/media/"):
                data=z.read(n)
                out=rf"D:\pmac资料\output\_rewrite_tmp\media\{k}_{n.split('/')[-1]}"
                open(out,"wb").write(data)
                print(out, len(data))
