# syntax=docker/dockerfile:1

# ---------- 1) Front-end (Vite + TypeScript) ----------
FROM node:26-alpine AS frontend
WORKDIR /src/Frontend

# Só os manifestos primeiro: a camada do `npm ci` só é refeita quando as dependências mudam
COPY Frontend/package.json Frontend/package-lock.json ./
RUN npm ci

COPY Frontend/ ./
# O Vite escreve em ../wwwroot (ou seja, /src/wwwroot)
RUN npm run build

# ---------- 2) Build do back-end (.NET) ----------
FROM mcr.microsoft.com/dotnet/sdk:11.0 AS build
WORKDIR /src

# Só o .csproj primeiro: o `dotnet restore` fica em cache enquanto os pacotes não mudam
COPY ComparadorNomes.csproj ./
RUN dotnet restore ComparadorNomes.csproj

# Restante do código (o .dockerignore deixa de fora Tests/, Frontend/node_modules, wwwroot local, etc.)
COPY . .
# Front compilado no estágio 1
COPY --from=frontend /src/wwwroot ./wwwroot
RUN dotnet publish ComparadorNomes.csproj -c Release -o /app/publish --no-restore

# ---------- 3) Imagem final ----------
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

# A aplicação lê a porta de PORT (o Render define; aqui o padrão é 5000).
# A imagem base define ASPNETCORE_HTTP_PORTS=8080; zerar evita o aviso de "porta sobrescrita".
ENV PORT=5000 \
    ASPNETCORE_HTTP_PORTS=
EXPOSE 5000

COPY --from=build /app/publish .

# Usuário sem privilégios (já existe nas imagens oficiais do .NET)
USER app

ENTRYPOINT ["dotnet", "ComparadorNomes.dll"]
