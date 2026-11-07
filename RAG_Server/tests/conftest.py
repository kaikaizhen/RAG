import os
import sys
from pathlib import Path

# 必須在匯入 main 之前設定：使用記憶體 Qdrant、固定測試金鑰、不預先載入模型
os.environ.setdefault("QDRANT_LOCATION", ":memory:")
os.environ["INTERNAL_API_KEY"] = "test-internal-key"
os.environ["RAG_DISABLE_MODEL_PRELOAD"] = "1"

sys.path.insert(0, str(Path(__file__).resolve().parent.parent))
