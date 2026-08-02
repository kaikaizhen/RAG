# 使用官方 Python 映像檔
FROM python:3.11-slim

# 設定工作目錄
WORKDIR /app

# 複製需求檔與程式碼進 container
COPY requirements.txt .
COPY main.py .

# 安裝套件
RUN pip install --no-cache-dir -r requirements.txt

# 對外開放 uvicorn 預設 port
EXPOSE 8007
# 啟動 FastAPI 應用
CMD ["uvicorn", "main:app", "--host", "0.0.0.0", "--port", "8007"]
