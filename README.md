# FileUploader

⚙️ Functionality:
 - Upload any type of file
 - List and download any uploaded file
 - Support for upload of big files without running into memory issues on the client


📄 Technical info:
 - **Blazor** used for client side
 - **PostgreSQL** used as DB
 - Files over 1 MB are uploaded in chunks (Tested with large files over 1 GB)
 - Small files under 1 MB are uploaded directly using a single stream of their data
 - Integration tests, including tests for big file uploads, files are random generated and checked if identical by comparing hashes


⚠️ In order to run the **App** you'll need:
 1. PostgreSQL (v17 preferably) installed with a superuser who can create dbs and read/write
 2. Replace your db username and password in the appsettings.Development.json config file in the FileUploader API project

⚠️ In order to run the **Tests** you'll need:
 1. Installed and running **Docker**
