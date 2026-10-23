# Admin Product Management Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Let authenticated Admin users list, add, edit, and safely delete store products, including uploading product images.

**Architecture:** Extend the existing Product entity with an optional image path and add an EF Core migration. Add role-protected product CRUD views under the existing Admin area; save validated, uniquely named image files under `wwwroot/images/products` and store only their public paths in SQL Server.

**Tech Stack:** ASP.NET Core MVC .NET 10, EF Core 10, SQL Server Express, ASP.NET Core Identity, Razor, xUnit.

**Spec:** User-approved scope in chat on 2026-10-23: Admin can create/edit/delete products, upload an image in the admin form, save products in the existing database, preserve order purchase snapshots, prevent deleting products already used by orders, and protect admin operations with the existing Admin login. Uploaded images are JPG/PNG/WebP up to 5 MB.

## Global Constraints

- Keep the project on `net10.0` and use the existing SQL Server Express `TechHavenSales` database.
- Require the existing `Admin` role for every product-management action.
- Never hard-code or expose the Admin password; keep the existing User Secrets setup.
- Preserve historical order item names and unit-price snapshots when a product changes.
- Reject deleting a product referenced by an order; do not cascade-delete order history.
- Accept only JPG, PNG, or WebP product images, at most 5 MB; generate server-side filenames.
- Keep uploaded images under `wwwroot/images/products`; never use the submitted filename as a storage path.
- Preserve existing storefront behavior and sample data; do not add payment, inventory, customer accounts, or other product fields.
- Update the SQL setup script with an idempotent additive column change and matching migration-history entry; EF migration startup remains the normal update path for existing databases.

## Review Focus

- An anonymous or non-Admin user must not list, create, edit, or delete products; exercise every route in product authorization tests.
- Invalid, oversized, or unsupported image uploads must not be written to disk or saved as product images.
- Replacing an image or posting an invalid form must not lose the existing product image; verify the stored file remains reachable.
- Deleting a product referenced by an order must be refused and leave both the order and product intact.
- Editing product name or price must not modify existing order-item purchase snapshots.

---

### Task 1: Add image metadata and database migration

**Files:**
- Modify: `Models/Product.cs`
- Modify: `Data/ApplicationDbContext.cs` only if needed for column configuration
- Create: `Data/Migrations/*_AddProductImageUrl.cs` and generated designer/snapshot updates
- Modify: `create-database.sql`
- Test: `final.Tests/Data/DbInitializerTests.cs` or focused catalog/data tests

**Interfaces:**
- `Product.ImageUrl`: nullable public path for the product image, e.g. `/images/products/<generated-name>.jpg`.
- Existing EF migration startup applies the additive nullable column to databases already at `InitialSales`.

- [x] **Step 1: Add a model/database test** that confirms a product can persist with a null image path and with an image path.
- [x] **Step 2: Run the focused test** with `dotnet test .\final.Tests\final.Tests.csproj --filter FullyQualifiedName~Product`; confirm the image property is not available or persisted yet.
- [x] **Step 3: Add `Product.ImageUrl` as a nullable string** and generate the `AddProductImageUrl` EF migration.
- [x] **Step 4: Update `create-database.sql`** to add nullable `ImageUrl` only when absent and record the `AddProductImageUrl` migration only when absent, so the script works for both fresh and existing databases without changing order schema.
- [x] **Step 5: Run focused data tests and build**; verify old products remain valid with a null image path.

### Task 2: Implement safe product-image upload and CRUD endpoints

**Files:**
- Create: `Areas/Admin/Controllers/ProductsController.cs`
- Create or modify: `ViewModels/AdminProductInputModel.cs`
- Create: `Areas/Admin/Views/Products/Index.cshtml`
- Create: `Areas/Admin/Views/Products/Create.cshtml`
- Create: `Areas/Admin/Views/Products/Edit.cshtml`
- Create: `Areas/Admin/Views/Products/Delete.cshtml`
- Create: `Services/ProductImageService.cs` only if needed to keep file validation/storage/removal out of the controller
- Test: `final.Tests/Products/ProductManagementTests.cs`

**Interfaces:**
- Admin endpoints: `GET /Admin/Products`, `GET /Admin/Products/Create`, `POST /Admin/Products/Create`, `GET /Admin/Products/Edit/{id}`, `POST /Admin/Products/Edit/{id}`, `GET /Admin/Products/Delete/{id}`, `POST /Admin/Products/Delete/{id}`.
- Product form fields: name, description, category, price, and optional image upload.
- Uploaded image paths use unique generated file names and are persisted to `Product.ImageUrl`.
- The storefront shows a product's uploaded image when present and retains its category icon as a fallback.
- Newly uploaded images are publicly served immediately without requiring an application restart.

- [x] **Step 1: Add failing integration tests** for Admin-only access to every route, create/edit persistence, rejected invalid product data, accepted JPG/PNG/WebP uploads, rejected unsupported/over-5-MB uploads, preserving the existing image on invalid edit or omitted replacement, unchanged order snapshots after product edits, and deletion refusal for products referenced by orders.
- [x] **Step 2: Run the focused product tests** and confirm the routes and upload behavior are not implemented.
- [x] **Step 3: Implement role-protected list/create/edit/delete actions and their Vietnamese Razor views** with Data Annotation validation and antiforgery validation on all POST actions.
- [x] **Step 4: Implement upload validation** for `.jpg`/`.jpeg`, `.png`, and `.webp` extensions plus matching image MIME types and a 5 MB limit; store under `wwwroot/images/products` using generated filenames.
- [x] **Step 5: Preserve the old image on edit when no new file is supplied; replace it only after a successful product save, then remove the previous generated upload.**
- [x] **Step 6: Refuse product deletion when any order item references it; otherwise require the confirmation page and antiforgery token.**
- [x] **Step 7: Run the focused product tests** and verify every security and persistence case passes.

### Task 3: Add Admin navigation, documentation, and end-to-end verification

**Files:**
- Modify: `Views/Shared/_Layout.cshtml`
- Modify: `Views/Home/_ProductCards.cshtml`
- Modify: `wwwroot/css/site.css`
- Modify: `Program.cs` if needed to serve newly uploaded files immediately
- Modify: `README.md`
- Test: `final.Tests/Products/ProductManagementTests.cs` and existing catalog/order tests

**Interfaces:**
- Admin navigation links to product management at `/Admin/Products`.
- Consumes the complete CRUD endpoints and views from Task 2.

- [x] **Step 1: Add view integration assertions** for Admin product navigation and forms including the file input and multipart encoding.
- [x] **Step 2: Build Vietnamese product list and create/edit/delete confirmation views** following existing Admin order page styling and antiforgery patterns.
- [x] **Step 3: Add an Admin-only product navigation link** without changing customer navigation.
- [x] **Step 4: Document the Admin product page and supported image types/size** in `README.md`.
- [x] **Step 5: Run `dotnet test .\final.Tests\final.Tests.csproj` and `dotnet build .\final.csproj`; verify all tests and build pass.**
- [x] **Step 6: Verify the generated migration applies to the existing SQL Server Express database and the product catalog still renders successfully.**
