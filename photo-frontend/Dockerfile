# Берём готовый nginx — он умеет отдавать статические файлы
FROM nginx:alpine

# Копируем index.html, js/, css/, images/, pages/ в папку nginx
COPY . /usr/share/nginx/html

EXPOSE 80