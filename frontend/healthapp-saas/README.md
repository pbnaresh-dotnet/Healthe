# HealthApp React SaaS Frontend

The frontend calls the real .NET 10 API. Mock/localStorage persistence is not used by the normal development path.

```bash
cd frontend/healthapp-saas
copy .env.example .env
npm install
npm run dev:customer
```

Apps: customer-web (5173), outlet-web (5174), admin-web (5175).

The API must be running at `http://localhost:50448`.
