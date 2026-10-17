"""Sinh sơ đồ ERD dạng draw.io (.drawio) từ schema JSON xuất ra từ SQL Server.

Cách dùng: python gen_drawio_erd.py <schema.json> <output.drawio>
schema.json: danh sách bảng, mỗi bảng có danh sách cột (name, type, maxLength,
precision, scale, nullable, pk, uq, refTable, refColumn).
File sinh ra mở bằng draw.io / app.diagrams.net và chỉnh sửa bình thường.
"""
import json
import sys
from xml.sax.saxutils import escape

# Vị trí (cột, hàng) của từng bảng, sắp theo luồng nghiệp vụ
GRID = {
    "Users": (0, 0), "Lecturers": (1, 0), "Departments": (2, 0), "Courses": (3, 0), "Semesters": (4, 0),
    "Students": (0, 1), "Warnings": (1, 1), "CourseSections": (3, 1),
    "Enrollments": (1, 2), "AttendanceRecords": (2, 2), "AttendanceSessions": (3, 2),
    "ModelVersions": (0, 3), "RiskPredictions": (1, 3),
}
WIDTH = 300
HEADER = 30
ROW = 22
GAP_X = 80
GAP_Y = 90
NBSP = " "

TABLE_STYLE = ("swimlane;fontStyle=1;childLayout=stackLayout;horizontal=1;startSize=30;horizontalStack=0;"
               "resizeParent=1;resizeParentMax=0;resizeLast=0;collapsible=0;marginBottom=0;html=1;"
               "fillColor=#dae8fc;strokeColor=#6c8ebf;rounded=1;arcSize=6;fontSize=13;")
ROW_STYLE = ("text;strokeColor=none;fillColor=none;align=left;verticalAlign=middle;spacingLeft=6;spacingRight=4;"
             "overflow=hidden;rotatable=0;points=[[0,0.5],[1,0.5]];portConstraint=eastwest;html=1;"
             "fontFamily=Courier New;fontSize=12;")
EDGE_STYLE = "edgeStyle=none;html=1;endFill=0;startFill=0;strokeWidth=1.5;strokeColor=#333333;"


def column_type(col):
    sql_type = col["type"].upper()
    if sql_type in ("NVARCHAR", "VARCHAR"):
        size = "MAX" if col["maxLength"] == -1 else col["maxLength"] // (2 if sql_type == "NVARCHAR" else 1)
        return f"{sql_type}({size})"
    if sql_type == "DECIMAL":
        return f'DECIMAL({col["precision"]},{col["scale"]})'
    return sql_type


def key_string(col):
    keys = []
    if col["pk"]:
        keys.append("PK")
    if col["refTable"]:
        keys.append("FK")
    if not col["pk"] and col["uq"]:
        keys.append("U")
    if not col["pk"] and col["nullable"]:
        keys.append("N")
    return ",".join(keys)


def row_label(col, name_width):
    key = key_string(col).ljust(6)
    name = col["name"].ljust(name_width + 1)
    text = escape(key + name + column_type(col)).replace(" ", NBSP)
    return f"<b><u>{text}</u></b>" if col["pk"] else text


def build(schema):
    tables = {t["table"]: t for t in schema if t["table"] in GRID}
    heights = {name: HEADER + ROW * len(t["columns"]) for name, t in tables.items()}

    # Chiều cao mỗi hàng = bảng cao nhất trong hàng đó
    row_tops, top = {}, 40
    for r in sorted({pos[1] for pos in GRID.values()}):
        row_tops[r] = top
        top += max(heights[n] for n, pos in GRID.items() if pos[1] == r) + GAP_Y

    cells = []
    for name, t in tables.items():
        c, r = GRID[name]
        x, y = 40 + c * (WIDTH + GAP_X), row_tops[r]
        cells.append(f'<mxCell id="{name}" value="{name}" style="{TABLE_STYLE}" vertex="1" parent="1">'
                     f'<mxGeometry x="{x}" y="{y}" width="{WIDTH}" height="{heights[name]}" as="geometry"/></mxCell>')
        name_width = max(len(col["name"]) for col in t["columns"])
        for i, col in enumerate(t["columns"]):
            cells.append(f'<mxCell id="{name}.{col["name"]}" value="{escape(row_label(col, name_width), {chr(34): "&quot;"})}" '
                         f'style="{ROW_STYLE}" vertex="1" parent="{name}">'
                         f'<mxGeometry y="{HEADER + i * ROW}" width="{WIDTH}" height="{ROW}" as="geometry"/></mxCell>')

    # Quan hệ: đầu bảng cha (1 hoặc 0..1), đầu bảng con (0..* hoặc 0..1 nếu khóa ngoại là duy nhất)
    for name, t in tables.items():
        for col in t["columns"]:
            if not col["refTable"]:
                continue
            start = "ERzeroToOne" if col["nullable"] else "ERmandOne"
            end = "ERzeroToOne" if col["uq"] else "ERzeroToMany"
            cells.append(f'<mxCell id="fk.{name}.{col["name"]}" style="{EDGE_STYLE}startArrow={start};endArrow={end};" '
                         f'edge="1" parent="1" source="{col["refTable"]}" target="{name}">'
                         f'<mxGeometry relative="1" as="geometry"/></mxCell>')

    body = "".join(cells)
    return ('<mxfile host="app.diagrams.net"><diagram id="erd-dacn" name="ERD - DACN">'
            '<mxGraphModel dx="1600" dy="1200" grid="1" gridSize="10" guides="1" tooltips="1" connect="1" arrows="1" '
            'fold="1" page="0" pageScale="1" math="0" shadow="0"><root><mxCell id="0"/><mxCell id="1" parent="0"/>'
            f'{body}</root></mxGraphModel></diagram></mxfile>')


if __name__ == "__main__":
    with open(sys.argv[1], encoding="utf-8-sig") as f:
        schema = json.load(f)
    with open(sys.argv[2], "w", encoding="utf-8") as f:
        f.write(build(schema))
    print("Created", sys.argv[2])
