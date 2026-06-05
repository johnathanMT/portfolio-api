# Step 1 — CLI Commands

Run these commands in order from your terminal.

---

## 1. Create the project

```bash
dotnet new webapi -n PortfolioApi --framework net8.0
cd PortfolioApi
```

## 2. Install all NuGet packages

```bash
# ORM & MySQL
dotnet add package Microsoft.EntityFrameworkCore --version 8.0.6
dotnet add package Microsoft.EntityFrameworkCore.Design --version 8.0.6
dotnet add package Pomelo.EntityFrameworkCore.MySql --version 8.0.2

# Authentication
dotnet add package Microsoft.AspNetCore.Authentication.JwtBearer --version 8.0.6

# Password hashing
dotnet add package BCrypt.Net-Next --version 4.0.3

# Swagger / OpenAPI
dotnet add package Swashbuckle.AspNetCore --version 6.7.3
dotnet add package Microsoft.AspNetCore.OpenApi --version 8.0.6

# Cloudinary image upload
dotnet add package CloudinaryDotNet --version 1.26.2

# Input validation
dotnet add package FluentValidation.AspNetCore --version 11.3.0
```

## 3. Install EF Core CLI tools (global, run once)

```bash
dotnet tool install --global dotnet-ef
```

## 4. Create initial migration (after setting up AppDbContext)

```bash
dotnet ef migrations add InitialCreate
dotnet ef database update
```

## 5. Run locally

```bash
dotnet run
# API available at: https://localhost:5001
# Swagger UI at:   https://localhost:5001/swagger
```

## 6. Build for production

```bash
dotnet publish -c Release -o ./publish
```

---

# Step 2 — Folder Structure

```
PortfolioApi/
│
├── Controllers/
│   ├── AuthController.cs          # Login, Register
│   └── ArticlesController.cs      # Full CRUD for articles
│
├── Data/
│   └── AppDbContext.cs            # EF Core DbContext
│
├── DTOs/
│   ├── Auth/
│   │   ├── LoginDto.cs
│   │   ├── RegisterDto.cs
│   │   └── AuthResponseDto.cs
│   └── Article/
│       ├── CreateArticleDto.cs
│       ├── UpdateArticleDto.cs
│       └── ArticleResponseDto.cs
│
├── Interfaces/
│   ├── IAuthService.cs
│   ├── IArticleService.cs
│   ├── IImageService.cs
│   ├── IUserRepository.cs
│   └── IArticleRepository.cs
│
├── Middleware/
│   └── ExceptionMiddleware.cs     # Global error handling
│
├── Models/
│   ├── User.cs
│   └── Article.cs
│
├── Repositories/
│   ├── UserRepository.cs
│   └── ArticleRepository.cs
│
├── Services/
│   ├── AuthService.cs
│   ├── ArticleService.cs
│   └── CloudinaryImageService.cs
│
├── Validators/
│   ├── LoginDtoValidator.cs
│   ├── RegisterDtoValidator.cs
│   └── CreateArticleDtoValidator.cs
│
├── Common/
│   └── ApiResponse.cs             # Standardised JSON envelope
│
├── appsettings.json
├── appsettings.Development.json
├── Program.cs
├── Dockerfile
└── .dockerignore
```
