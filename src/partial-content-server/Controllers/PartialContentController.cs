using System.Buffers;
using System.Net.Http.Headers;
using System.Net.Mime;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.Mvc;

namespace partial_content_server.Controllers;

[ApiController]
[Route("[controller]")]
public class PartialContentController : ControllerBase
{
    private readonly ILogger<PartialContentController> _logger;

    public PartialContentController(ILogger<PartialContentController> logger)
    {
        _logger = logger;
    }

    // THIS IS A BAD IMPLEMENTATION, DO NOT USE IN REAL PROJECT!
    [HttpGet("sprite-images", Name = "GetPartialContentSpriteImages")]
    public async Task GetSpriteImages([FromQuery] int? index, CancellationToken token = default)
    {
        Response.StatusCode = StatusCodes.Status206PartialContent;

        // ---------------------------------------
        // // The most acceptable workaround is, return 200(OK) to let browser know it's safe content
        // Response.StatusCode = StatusCodes.Status200OK;
        // ---------------------------------------

        var imageStream = Assets.GetImageStream();
        var (offset, length) = Assets.GetImageOffsetAndLength(index);

        var imageBytes = ArrayPool<byte>.Shared.Rent(length);
        imageStream.Seek(offset, SeekOrigin.Begin);
        var readLength = await imageStream.ReadAsync(imageBytes.AsMemory(0, length), token);

        Response.ContentType = MediaTypeNames.Application.Octet; // application/octec, default binary
        // ---------------------------------------
        // // Yet another fix point, use specified MIME type to pass browser sandbox detecting
        // Response.ContentType = MediaTypeNames.Image.Jpeg;
        // ---------------------------------------
        Response.ContentLength = readLength;

        await StreamCopyOperation.CopyToAsync(new MemoryStream(imageBytes), Response.Body, readLength,
            bufferSize: 64 * 1024, token);

        _logger.LogInformation("Response sprite image by {Index}, size is {SizeInBytes} kB",
            index,
            readLength / 1000);

        await Response.CompleteAsync();
    }

    // THIS IS A SIMULATED IMPLEMENTATION FOR DEMONSTRATE HOW RANGE WORK, DO NOT USE IN REAL PROJECTS
    [HttpGet("images", Name = "GetPartialContentImages")]
    public async Task GetImages(CancellationToken token = default)
    {
        // Consider below three main cases:
        //    [] indicates the whole resource
        //    | indicates the split point
        //    (200) indicates the response code
        //    (SUCCEED) indicates the result from client
        //
        // [ ------------------------ (200) ] (SUCCEED)
        // [ ---------- (206) | ========== (206) | ****** (200) ] (SUCCEED)
        // [ ============ (416) ] (FAIL)

        Response.ContentType = MediaTypeNames.Image.Jpeg;

        // var status = StatusCodes.Status206PartialContent;
        var rangeHeader = Request.GetTypedHeaders().Range;
        switch (rangeHeader?.Ranges.Count)
        {
            case 1:
                {
                    // single part
                    var resourceStream = Assets.Img10_Jpg!;

                    var firstRange = rangeHeader.Ranges.First();
                    var rangeStart = firstRange.From ?? 0;
                    var rangeEnd = firstRange.To ?? resourceStream.Length;
                    var length = (int)(rangeEnd - rangeStart);

                    if (rangeStart < 0 || rangeEnd > resourceStream.Length)
                    {
                        Response.StatusCode = StatusCodes.Status416RangeNotSatisfiable;
                        Response.Headers.ContentRange = $"*/{resourceStream.Length}";

                        _logger.LogWarning("The request {Range} is invalid, it should be {From} to {To}",
                            firstRange,
                            0,
                            resourceStream.Length);
                        break;
                    }

                    Response.StatusCode = rangeEnd == resourceStream.Length
                        ? StatusCodes.Status200OK
                        : StatusCodes.Status206PartialContent;

                    var imageBytes = ArrayPool<byte>.Shared.Rent(length);
                    var readLength = await resourceStream.ReadAsync(
                        imageBytes.AsMemory((int)rangeStart, length), token);

                    Response.Headers.ContentRange =
                        $"bytes {rangeStart}-{rangeStart + readLength}/{resourceStream.Length}";
                    Response.ContentLength = readLength;

                    await StreamCopyOperation.CopyToAsync(new MemoryStream(imageBytes),
                        Response.Body,
                        readLength,
                        bufferSize: 64 * 1024,
                        token);

                    _logger.LogInformation("Respond a single {Range} with {ContentRange}",
                        firstRange,
                        $"bytes {rangeStart}-{rangeStart + readLength}/{resourceStream.Length}");
                }
                break;
            case > 1:
                {
                    // multiple parts
                    var resourceStream = Assets.Img10_Jpg!;
                    var normalizedRanges = rangeHeader.Ranges.Select(r =>
                            new RangeItemHeaderValue(r.From ?? 0, r.To ?? resourceStream.Length))
                        .ToList();

                    if (normalizedRanges.Any(r =>
                            r.From! < 0 || r.To! - r.From! > resourceStream.Length))
                    {
                        Response.StatusCode = StatusCodes.Status416RangeNotSatisfiable;
                        Response.Headers.ContentRange = $"*/{resourceStream.Length}";

                        _logger.LogWarning("The request {Ranges} are invalid, it should be {From} to {To}",
                            normalizedRanges.Select(r =>
                                r.From! < 0 || r.To! - r.From! > resourceStream.Length).ToList(),
                            0,
                            resourceStream.Length);
                        break;
                    }

                    Response.StatusCode = StatusCodes.Status206PartialContent;
                    var content = new MultipartContent("multipart/byteranges", "THIS_STRING_SEPARATES");

                    foreach (var range in MergeRanges(normalizedRanges))
                    {
                        var rangeStart = range.From!.Value;
                        var rangeEnd = range.To!.Value;
                        var length = (int)(rangeEnd - rangeStart);

                        var imageBytes = ArrayPool<byte>.Shared.Rent(length);
                        var readLength = await resourceStream.ReadAsync(
                            imageBytes.AsMemory((int)rangeStart, length), token);

                        var contentPart = new ByteArrayContent(imageBytes);
                        contentPart.Headers.ContentType = MediaTypeHeaderValue.Parse(MediaTypeNames.Image.Jpeg);
                        contentPart.Headers.ContentLength = readLength;
                        content.Add(contentPart);
                    }

                    await content.CopyToAsync(Response.Body, token);
                }

                break;
            default:
                {
                    // no range in header
                    var resourceStream = Assets.Img10_Jpg!;
                    var length = (int)resourceStream.Length;

                    var imageBytes = ArrayPool<byte>.Shared.Rent(length);
                    var readLength = await resourceStream.ReadAsync(
                        imageBytes.AsMemory(0, length), token);

                    Response.StatusCode = StatusCodes.Status206PartialContent;
                    Response.ContentLength = readLength;
                    Response.Headers.ContentRange =
                        $"bytes {0}-{readLength}/{resourceStream.Length}";

                    await StreamCopyOperation.CopyToAsync(new MemoryStream(imageBytes),
                        Response.Body,
                        readLength,
                        bufferSize: 64 * 1024,
                        token);
                }
                break;
        }

        await Response.CompleteAsync();
    }

    private static IEnumerable<RangeItemHeaderValue> MergeRanges(IList<RangeItemHeaderValue> ranges)
    {
        var currentRange = ranges.First();
        for (var i = 1; i < ranges.Count; i++)
        {
            var mergingRange = ranges[i];

            if (mergingRange.From!.Value > currentRange.To!.Value) // no overlap
            {
                yield return currentRange;
                currentRange = mergingRange;
                continue;
            }

            currentRange = new RangeItemHeaderValue(
                Math.Min(currentRange.From!.Value, mergingRange.From!.Value),
                Math.Max(currentRange.To!.Value, mergingRange.To!.Value));
        }
    }

    [HttpGet("video", Name = "GetPartialContentVideo")]
    public IActionResult GetVideo()
    {
        // A quick implementation from ASP.NET core in which internal use FileStreamResult to response.
        // THIS IS MOST RECOMMENDED WAY FOR LARGE RESOURCES
        // NOTE:
        // 1. use File(...) with enableRangeProcessing to true means it default support RANGE header,
        //    and auto-detect to response 206 if need transfer by small chunks,
        //    and might 416 if RANGE is not satisfied when verify RANGE header.
        // 2. this also can support download when given fileDownloadName in overloads
        return File(Assets.Grade4_Mp3!,
            "audio/mpeg",
            enableRangeProcessing: true,
            fileDownloadName: "Grade4.mp3"
        );
    }
}