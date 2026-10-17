"""Sinh file StarUML (.mdj) chứa sơ đồ ERD từ schema JSON xuất ra từ SQL Server.

Cách dùng: python gen_staruml_erd.py <schema.json> <output.mdj>
schema.json: danh sách bảng, mỗi bảng có danh sách cột (name, type, maxLength,
precision, scale, nullable, pk, uq, refTable, refColumn).
"""
import base64
import json
import os
import sys

# Vị trí (x, y) của từng bảng trên sơ đồ, sắp theo luồng nghiệp vụ
LAYOUT = {
    "Users": (40, 40),
    "Lecturers": (420, 40),
    "Departments": (800, 40),
    "Courses": (1180, 40),
    "Semesters": (1560, 40),
    "Students": (40, 400),
    "Warnings": (420, 400),
    "CourseSections": (1180, 400),
    "Enrollments": (420, 760),
    "AttendanceRecords": (800, 820),
    "AttendanceSessions": (1180, 820),
    "ModelVersions": (40, 1120),
    "RiskPredictions": (420, 1120),
}
ENTITY_WIDTH = 300
ROW_HEIGHT = 18
LS_OBLIQUE = 1   # đường thẳng, không cắt qua bảng như kiểu bo góc


def new_id():
    return base64.b64encode(os.urandom(15)).decode()


def ref(obj_id):
    return {"$ref": obj_id}


def column_type(col):
    sql_type = col["type"].upper()
    if sql_type in ("NVARCHAR", "VARCHAR"):
        length = "MAX" if col["maxLength"] == -1 else str(col["maxLength"] // (2 if sql_type == "NVARCHAR" else 1))
        return sql_type, length
    if sql_type == "DECIMAL":
        return sql_type, f'{col["precision"]},{col["scale"]}'
    return sql_type, ""


def build(schema):
    project_id, model_id, diagram_id = new_id(), new_id(), new_id()
    tables = [t for t in schema if t["table"] in LAYOUT]

    entities, entity_ids, column_ids = [], {}, {}
    for t in tables:
        entity_ids[t["table"]] = new_id()
        for c in t["columns"]:
            column_ids[(t["table"], c["name"])] = new_id()

    views, view_ids, heights = [], {}, {}
    for t in tables:
        name, eid = t["table"], entity_ids[t["table"]]
        columns = []
        for c in t["columns"]:
            sql_type, length = column_type(c)
            col = {
                "_type": "ERDColumn",
                "_id": column_ids[(name, c["name"])],
                "_parent": ref(eid),
                "name": c["name"],
                "type": sql_type,
                "length": length,
                "primaryKey": bool(c["pk"]),
                "foreignKey": c["refTable"] is not None,
                "nullable": bool(c["nullable"]),
                "unique": bool(c["uq"]),
            }
            if c["refTable"]:
                col["referenceTo"] = ref(column_ids[(c["refTable"], c["refColumn"])])
            columns.append(col)

        # Quan hệ do bảng con (bảng có khóa ngoại) sở hữu: end1 = bảng cha, end2 = bảng con
        relationships = []
        for c in t["columns"]:
            if not c["refTable"]:
                continue
            rid = new_id()
            relationships.append({
                "_type": "ERDRelationship",
                "_id": rid,
                "_parent": ref(eid),
                "identifying": False,
                "end1": {
                    "_type": "ERDRelationshipEnd",
                    "_id": new_id(),
                    "_parent": ref(rid),
                    "reference": ref(entity_ids[c["refTable"]]),
                    "cardinality": "0..1" if c["nullable"] else "1",
                },
                "end2": {
                    "_type": "ERDRelationshipEnd",
                    "_id": new_id(),
                    "_parent": ref(rid),
                    "reference": ref(eid),
                    "cardinality": "0..1" if c["uq"] else "0..*",
                },
            })

        entities.append({
            "_type": "ERDEntity",
            "_id": eid,
            "_parent": ref(model_id),
            "name": name,
            "ownedElements": relationships,
            "columns": columns,
        })

        x, y = LAYOUT[name]
        heights[name] = 30 + ROW_HEIGHT * len(columns)
        view_ids[name] = new_id()
        views.append({
            "_type": "ERDEntityView",
            "_id": view_ids[name],
            "_parent": ref(diagram_id),
            "model": ref(eid),
            "left": x,
            "top": y,
            "width": ENTITY_WIDTH,
            "height": heights[name],
        })

    def center(name):
        x, y = LAYOUT[name]
        return x + ENTITY_WIDTH // 2, y + heights[name] // 2

    for entity in entities:
        for rel in entity["ownedElements"]:
            parent = next(n for n, i in entity_ids.items() if i == rel["end1"]["reference"]["$ref"])
            child = entity["name"]
            (x1, y1), (x2, y2) = center(parent), center(child)
            views.append({
                "_type": "ERDRelationshipView",
                "_id": new_id(),
                "_parent": ref(diagram_id),
                "model": ref(rel["_id"]),
                "tail": ref(view_ids[parent]),
                "head": ref(view_ids[child]),
                "points": f"{x1}:{y1};{x2}:{y2}",
                "lineStyle": LS_OBLIQUE,
            })

    diagram = {
        "_type": "ERDDiagram",
        "_id": diagram_id,
        "_parent": ref(model_id),
        "name": "ERD - DACN",
        "visible": True,
        "defaultDiagram": True,
        "ownedViews": views,
    }
    return {
        "_type": "Project",
        "_id": project_id,
        "name": "DACN - He thong canh bao sinh vien",
        "ownedElements": [{
            "_type": "ERDDataModel",
            "_id": model_id,
            "_parent": ref(project_id),
            "name": "Data Model",
            "ownedElements": [diagram] + entities,
        }],
    }


if __name__ == "__main__":
    with open(sys.argv[1], encoding="utf-8-sig") as f:
        schema = json.load(f)
    with open(sys.argv[2], "w", encoding="utf-8") as f:
        json.dump(build(schema), f, ensure_ascii=False, indent="\t")
    print("Created", sys.argv[2])
