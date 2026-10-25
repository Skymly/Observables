# Third-Party Notices

Observables uses or is derived from the following third-party projects.

## Refit

The `Observables.RestAPI` domain contains code adapted from **Refit**.

- Repository: https://github.com/reactiveui/refit
- License: MIT
- Copyright: Copyright (c) ReactiveUI 2012 - 2025
- Adaptation baseline: `v10.1.6` (2026-03-21) — the latest Refit release at the
  time the domain was adapted (introduced in commit `3929a95`, 2026-06-01).

The adapted code includes, but is not limited to, the HTTP method/parameter
attributes (`GetAttribute`, `PostAttribute`, `PutAttribute`, `DeleteAttribute`,
`PatchAttribute`, `HeadAttribute`, `OptionsAttribute`, `BodyAttribute`,
`QueryAttribute`, `HeaderAttribute`, `HeaderCollectionAttribute`,
`AuthorizeAttribute`, `PropertyAttribute`, `AliasAsAttribute`,
`MultipartAttribute`, `HeadersAttribute`, `QueryUriFormatAttribute`,
`AttachmentNameAttribute`), `RestService`, `RestApiSettings`, `RestApiBridge`,
`ApiResponse`, `ApiException`, `ApiRequestException`, `ApiExceptionBase`,
`ValidationApiException`, `FormValueMultimap`, `MultipartItem`,
`SystemTextJsonContentSerializer`, and related request-building infrastructure.

Refit is a trademark of its respective authors and is not affiliated with the
Observables project.

## MIT License Notice

```
MIT License

Copyright (c) ReactiveUI 2012 - 2025

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
```
