param(
    [string]$BaseUrl = "http://localhost:5244",
    [string]$Password = "MomCare@123"
)

$ErrorActionPreference = "Stop"

function Login([string]$Email) {
    $body = @{
        email = $Email
        password = $Password
    } | ConvertTo-Json

    return (Invoke-RestMethod -Method Post -Uri "$BaseUrl/api/Auth/login" -ContentType "application/json" -Body $body).accessToken
}

function Invoke-Api([string]$Method, [string]$Path, [string]$Token, $Body = $null) {
    $headers = @{ Authorization = "Bearer $Token" }

    if ($null -ne $Body) {
        $payload = $Body | ConvertTo-Json -Depth 10
        return Invoke-RestMethod -Method $Method -Uri "$BaseUrl$Path" -Headers $headers -ContentType "application/json" -Body $payload
    }

    return Invoke-RestMethod -Method $Method -Uri "$BaseUrl$Path" -Headers $headers
}

$customerToken = Login "lan.customer@momcare.local"
$nurseToken = Login "huong.nurse@momcare.local"
$adminToken = Login "admin@momcare.local"

$results = [System.Collections.Generic.List[object]]::new()

$results.Add([pscustomobject]@{
    Check = "Customer me"
    Result = Invoke-Api GET "/api/Auth/me" $customerToken
})

$results.Add([pscustomobject]@{
    Check = "Customer notifications"
    Result = Invoke-Api GET "/api/notifications/mine/unread-count" $customerToken
})

$results.Add([pscustomobject]@{
    Check = "Customer services"
    Result = Invoke-RestMethod -Method Get -Uri "$BaseUrl/api/services"
})

$results.Add([pscustomobject]@{
    Check = "Customer bookings"
    Result = Invoke-Api GET "/api/bookings/my/customer" $customerToken
})

$results.Add([pscustomobject]@{
    Check = "Nurse bookings"
    Result = Invoke-Api GET "/api/bookings/my/nurse" $nurseToken
})

$results.Add([pscustomobject]@{
    Check = "Admin dashboard"
    Result = Invoke-Api GET "/api/Admin/dashboard" $adminToken
})

$results.Add([pscustomobject]@{
    Check = "Notification hub negotiate"
    Result = Invoke-Api POST "/hubs/notifications/negotiate?negotiateVersion=1" $customerToken
})

$results.Add([pscustomobject]@{
    Check = "Chat hub negotiate"
    Result = Invoke-Api POST "/hubs/chat/negotiate?negotiateVersion=1" $customerToken
})

$results | ForEach-Object {
    Write-Host "=== $($_.Check) ==="
    ($_.Result | ConvertTo-Json -Depth 10)
    Write-Host ""
}
