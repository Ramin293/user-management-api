# User Management API

An ASP.NET Core REST API with user CRUD, input validation, duplicate-email checks, and HTTP request logging. The in-memory store starts empty and resets when the app restarts.

## Run locally

Install the .NET 10 SDK and run this command from the project directory.

```sh
dotnet run --urls http://localhost:5080
```

| Method | Route | Success |
| --- | --- | --- |
| GET | /api/users | 200 with the list |
| GET | /api/users/{id} | 200 with the user |
| POST | /api/users | 201 with the user and a Location header |
| PUT | /api/users/{id} | 200 with the updated user |
| DELETE | /api/users/{id} | 204 |

POST and PUT accept an application/json body. PUT requires both fields.

```json
{"name":"Alex Rivera","email":"alex@example.com"}
```

Invalid input returns 400. Missing users return 404. Duplicate email addresses return 409, including case-only variants. The middleware logs the method, path, response status and elapsed time.
